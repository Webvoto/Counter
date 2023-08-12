using CsvHelper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Counter {

	public class VoteCounter {

		private record Vote(int PoolId, int Slot, byte[] EncodedValue, byte[] CmsSignature, byte[] ServerSignature, int ServerInstanceId, EncodedVote Value);

		private const int BatchSize = 1000;

		private readonly HttpClient httpClient = new HttpClient();
		private readonly Dictionary<int, RSA> serverPublicKeys = new Dictionary<int, RSA>();
		private WebVaultKeyParameters decryptionKeyParams;
		private WebVaultClient webVaultClient;
		private X509Certificate2 signatureCertificate;
		private RSA signatureCertificatePublicKey;

		public void Initialize(FileInfo decryptionKeyParamsFile, FileInfo signatureCertificateFile) {
			decryptionKeyParams = WebVaultKeyParameters.Deserialize(File.ReadAllText(decryptionKeyParamsFile.FullName));
			webVaultClient = new WebVaultClient(httpClient, decryptionKeyParams.Endpoint, decryptionKeyParams.ApiKey);
			signatureCertificate = new X509Certificate2(File.ReadAllBytes(signatureCertificateFile.FullName));
			signatureCertificatePublicKey = signatureCertificate.GetRSAPublicKey();
		}

		public async Task<ElectionResultCollection> CountAsync(FileInfo votesCsvFile, FileInfo partiesCsvFile = null) {

			var results = new ElectionResultCollection();

			if (partiesCsvFile != null) {
				using var partyCsvReader = PartiesCsvReader.Open(partiesCsvFile);
				foreach (var party in partyCsvReader.GetRecords()) {
					results
						.GetOrAddElection(party.ElectionId, () => new ElectionResult(party.ElectionId, party.ElectionName))
						.GetOrAddParty(party.PartyId, () => new PartyResult(party.PartyId, party.PartyName, !string.IsNullOrEmpty(party.PartyNumber) ? int.Parse(party.PartyNumber) : null));
				}
			}

			using (var votesCsvReader = VotesCsvReader.Open(votesCsvFile)) {
				foreach (var voteRecord in votesCsvReader.GetRecords()) {
					if (!serverPublicKeys.ContainsKey(voteRecord.ServerInstanceId)) {
						serverPublicKeys[voteRecord.ServerInstanceId] = getPublicKey(decodeHexString(voteRecord.ServerPublicKey));
					}
				}
			}

			using (var votesCsvReader = VotesCsvReader.Open(votesCsvFile)) {
				var voteBatch = new List<Vote>();
				foreach (var voteRecord in votesCsvReader.GetRecords()) {
					voteBatch.Add(decodeVote(voteRecord));
					if (voteBatch.Count == BatchSize) {
						await countVoteBatchAsync(results, voteBatch);
						Console.Write($".");
						voteBatch.Clear();
					}
				}
				if (voteBatch.Any()) {
					await countVoteBatchAsync(results, voteBatch);
					Console.Write($".");
				}
			}
			Console.WriteLine($" DONE");

			return results;
		}

		private Vote decodeVote(VoteCsvRecord csvEntry) {
			var poolId = csvEntry.PoolId;
			var slot = csvEntry.Slot;
			var encodedValue = decodeHexString(csvEntry.Value);
			var cmsSignature = decodeHexString(csvEntry.CmsSignature);
			var serverSignature = decodeHexString(csvEntry.ServerSignature);
			var value = VoteEncoding.Decode(encodedValue);
			return new Vote(poolId, slot, encodedValue, cmsSignature, serverSignature, csvEntry.ServerInstanceId, value);
		}

		private async Task countVoteBatchAsync(ElectionResultCollection results, List<Vote> votes) {

			var choiceDecryptions = await decryptChoicesAsync(votes);

			foreach (var vote in votes) {
				checkVote(vote);
				countVote(results, choiceDecryptions, vote);
			}
		}

		private async Task<DecryptionTable> decryptChoicesAsync(List<Vote> votes) {
			var ciphers = votes.SelectMany(v => v.Value.Choices.Select(c => c.EncryptedChoice));
			var plaintexts = await webVaultClient.DecryptBatchAsync(decryptionKeyParams.KeyId, ciphers);
			return new DecryptionTable(ciphers, plaintexts);
		}

		private void checkVote(Vote vote) {

			// Check server signature
			var serverSigOk = verifyServerSignature(serverPublicKeys[vote.ServerInstanceId], vote.CmsSignature, vote.ServerSignature)
				|| serverPublicKeys.Any(pk => verifyServerSignature(pk.Value, vote.CmsSignature, vote.ServerSignature));
			if (!serverSigOk) {
				throw new Exception($"Vote on pool {vote.PoolId} slot {vote.Slot} has an invalid server signature");
			}

			var cmsInfo = CmsEncoding.Decode(vote.CmsSignature);

			var expectedMessageDigestValue = HashAlgorithm.Create(cmsInfo.MessageDigest.Algorithm.Name).ComputeHash(vote.EncodedValue);
			if (!cmsInfo.MessageDigest.Value.SequenceEqual(expectedMessageDigestValue)) {
				throw new Exception("Message digest mismatch");
			}

			// Check signing certificate
			var expectedCertDigestValue = HashAlgorithm.Create(cmsInfo.SigningCertificateDigest.Algorithm.Name).ComputeHash(signatureCertificate.RawData);
			if (!cmsInfo.SigningCertificateDigest.Value.SequenceEqual(expectedCertDigestValue)) {
				throw new Exception("Signing certificate mismatch");
			}

			// Check signature of signed attributes
			if (!signatureCertificatePublicKey.VerifyHash(cmsInfo.SignedAttributesDigest.Value, cmsInfo.Signature, cmsInfo.SignedAttributesDigest.Algorithm, RSASignaturePadding.Pkcs1)) {
				throw new Exception("Signature mismatch");
			}

			// Check PoolId and Slot integrity
			if (vote.PoolId != vote.Value.PoolId || vote.Slot != vote.Value.Slot) {
				throw new Exception("Vote address corruption");
			}
		}

		private bool verifyServerSignature(RSA serverPublicKey, byte[] data, byte[] signature)
			=> serverPublicKey.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

		private void countVote(ElectionResultCollection results, DecryptionTable choiceDecryptions, Vote vote) {
			foreach (var choice in vote.Value.Choices) {
				var decryptedChoice = Encoding.UTF8.GetString(choiceDecryptions.GetDecryption(choice.EncryptedChoice));
				results.GetOrAddElection(choice.ElectionId).GetOrAddParty(decryptedChoice).Increment();
			}
		}

		#region Helper methods

		private static byte[] decodeHexString(string s) {
			if (s == null) {
				throw new ArgumentNullException(nameof(s));
			}
			if (s.Length % 2 != 0) {
				throw new Exception("Invalid hex string: bad length");
			}
			var result = new byte[s.Length / 2];
			for (var i = 0; i < result.Length; i++) {
				result[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
			}
			return result;
		}

		private RSA getPublicKey(byte[] encodedPublicKey) {
			var rsa = RSA.Create();
			rsa.ImportSubjectPublicKeyInfo(encodedPublicKey, out _);
			return rsa;
		}

		#endregion
	}
}
