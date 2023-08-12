using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Counter {

	public class VoteCounter {

		private record Vote(int PoolId, int Slot, byte[] EncodedValue, byte[] CmsSignature, byte[] ServerSignature, int ServerInstanceId, Asn1Vote Value);

		private class VoteBatch {

			public int Index { get; }

			public List<Vote> EncryptedVotes { get; }

			public DecryptionTable DecryptionTable { get; set; }

			public VoteBatch(int index, IEnumerable<Vote> encryptedVotes) {
				Index = index;
				EncryptedVotes = new List<Vote>(encryptedVotes);
			}
		}

		private const int BatchSize = 1000;

		private readonly Dictionary<int, RSA> serverPublicKeys = new Dictionary<int, RSA>();
		private WebVaultKeyParameters decryptionKeyParams;
		private WebVaultClient webVaultClient;
		private X509Certificate2 signatureCertificate;
		private RSA signatureCertificatePublicKey;

		public void Initialize(FileInfo signatureCertificateFile, FileInfo decryptionKeyParamsFile) {
			Console.WriteLine("Initializing ...");
			signatureCertificate = new X509Certificate2(File.ReadAllBytes(signatureCertificateFile.FullName));
			signatureCertificatePublicKey = signatureCertificate.GetRSAPublicKey();
			decryptionKeyParams = WebVaultKeyParameters.Deserialize(File.ReadAllText(decryptionKeyParamsFile.FullName));
			webVaultClient = new WebVaultClient(decryptionKeyParams.Endpoint, decryptionKeyParams.ApiKey);
		}

		public async Task<ElectionResultCollection> CountAsync(FileInfo votesCsvFile, FileInfo partiesCsvFile, int degreeOfParallelism) {

			var results = new ElectionResultCollection();

			if (partiesCsvFile != null) {
				Console.WriteLine("Reading parties ...");
				using var partyCsvReader = PartiesCsvReader.Open(partiesCsvFile);
				foreach (var party in partyCsvReader.GetRecords()) {
					results
						.GetOrAddElection(party.ElectionId, () => new ElectionResult(party.ElectionId, party.ElectionName))
						.GetOrAddParty(party.PartyId, () => new PartyResult(party.PartyId, party.PartyName, !string.IsNullOrEmpty(party.PartyNumber) ? int.Parse(party.PartyNumber) : null));
				}
			}

			Console.Write("Reading server keys ...");
			var voteIndex = 0;
			using (var votesCsvReader = VotesCsvReader.Open(votesCsvFile)) {
				foreach (var voteRecord in votesCsvReader.GetRecords()) {
					if (!serverPublicKeys.ContainsKey(voteRecord.ServerInstanceId)) {
						serverPublicKeys[voteRecord.ServerInstanceId] = getPublicKey(decodeHexString(voteRecord.ServerPublicKey));
						Console.Write(".");
					}
					if (++voteIndex % 1000 == 0) {
						Console.Write(".");
					}
				}
			}
			Console.WriteLine();

			var decryptionQueue = Channel.CreateBounded<VoteBatch>(degreeOfParallelism);
			var countingQueue = Channel.CreateBounded<VoteBatch>(degreeOfParallelism);
			var decryptionTasks = new List<Task>();
			var countingTasks = new List<Task>();

			for (var i = 0; i < degreeOfParallelism; i++) {
				decryptionTasks.Add(Task.Run(() => decryptVotesAsync(decryptionQueue.Reader, countingQueue.Writer)));
				countingTasks.Add(Task.Run(() => countVotesAsync(countingQueue.Reader, results)));
			}

			Console.Write("Counting votes ... ");

			await readVotesAsync(votesCsvFile, decryptionQueue.Writer);
			
			decryptionQueue.Writer.Complete();
			await Task.WhenAll(decryptionTasks);

			countingQueue.Writer.Complete();
			await Task.WhenAll(countingTasks);

			Console.WriteLine($" DONE");

			return results;
		}

		private async Task readVotesAsync(FileInfo votesCsvFile, ChannelWriter<VoteBatch> outQueue) {
			var batchIndex = 0;
			var votes = new List<Vote>();
			using var votesCsvReader = VotesCsvReader.Open(votesCsvFile);
			foreach (var voteRecord in votesCsvReader.GetRecords()) {
				votes.Add(decodeVote(voteRecord));
				if (votes.Count == BatchSize) {
					var batch = new VoteBatch(batchIndex++, votes);
					Console.Write($"[{batch.Index:D3}R]");
					await outQueue.WriteAsync(batch);
					votes.Clear();
				}
			}
			if (votes.Any()) {
				var batch = new VoteBatch(batchIndex++, votes);
				Console.Write($"[{batch.Index:D3}R]");
				await outQueue.WriteAsync(batch);
			}
		}

		private async Task decryptVotesAsync(ChannelReader<VoteBatch> inQueue, ChannelWriter<VoteBatch> outQueue) {
			await foreach (var batch in inQueue.ReadAllAsync()) {
				batch.DecryptionTable = await decryptChoicesAsync(batch.EncryptedVotes);
				Console.Write($"[{batch.Index:D3}D]");
				await outQueue.WriteAsync(batch);
			}
		}

		private async Task countVotesAsync(ChannelReader<VoteBatch> inQueue, ElectionResultCollection results) {
			await foreach (var batch in inQueue.ReadAllAsync()) {
				foreach (var vote in batch.EncryptedVotes) {
					checkVote(vote);
					countVote(results, batch.DecryptionTable, vote);
				}
				Console.Write($"[{batch.Index:D3}C]");
			}
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
