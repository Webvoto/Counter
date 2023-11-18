﻿using System;
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
		private RSA decryptionKey;
		private WebVaultKeyParameters decryptionKeyParams;
		private WebVaultClient webVaultClient;
		private X509Certificate2 signatureCertificate;
		private RSA signatureCertificatePublicKey;

		public void Initialize(FileInfo signatureCertificateFile, FileInfo decryptionKeyFile) {
			Console.WriteLine("Initializing ...");
			signatureCertificate = new X509Certificate2(File.ReadAllBytes(signatureCertificateFile.FullName));
			signatureCertificatePublicKey = signatureCertificate.GetRSAPublicKey();
			if (decryptionKeyFile != null) {
				if (decryptionKeyFile.Extension.Equals(".pem", StringComparison.InvariantCultureIgnoreCase)) {
					initializeLocalDecryptionKey(decryptionKeyFile);
				} else if (decryptionKeyFile.Extension.Equals(".json", StringComparison.InvariantCultureIgnoreCase)) {
					initializeRemoteDecryptionKey(decryptionKeyFile);
				} else {
					throw new Exception($"Unexpected decryption key file extension: '{decryptionKeyFile.Extension}'");
				}
			}
		}

		private void initializeLocalDecryptionKey(FileInfo decryptionKeyFile) {
			var decryptionKeyPkcs8Pem = File.ReadAllBytes(decryptionKeyFile.FullName);
			var decryptionKeyPkcs8Bytes = Util.DecodePem(decryptionKeyPkcs8Pem);
			
			Console.WriteLine("Please provide the decryption key file password:");
			var password = Console.ReadLine();
			
			decryptionKey = RSA.Create();
			decryptionKey.ImportEncryptedPkcs8PrivateKey(Encoding.UTF8.GetBytes(password), decryptionKeyPkcs8Bytes, out _);
		}

		private void initializeRemoteDecryptionKey(FileInfo decryptionKeyFile) {
			decryptionKeyParams = WebVaultKeyParameters.Deserialize(File.ReadAllText(decryptionKeyFile.FullName));
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
						.GetOrAddParty(party.PartyId, () => new PartyResult(party.PartyId, party.PartyName, !string.IsNullOrEmpty(party.PartyNumber) && !party.PartyNumber.Equals("NULL", StringComparison.InvariantCultureIgnoreCase) ? int.Parse(party.PartyNumber) : null));
				}
			}

			Console.Write("Reading server keys ...");
			var voteIndex = 0;
			using (var votesCsvReader = VotesCsvReader.Open(votesCsvFile)) {
				foreach (var voteRecord in votesCsvReader.GetRecords()) {
					if (!serverPublicKeys.ContainsKey(voteRecord.ServerInstanceId)) {
						serverPublicKeys[voteRecord.ServerInstanceId] = getPublicKey(Util.DecodeHex(voteRecord.ServerPublicKey));
						Console.Write(".");
					}
					if (++voteIndex % 1000 == 0) {
						Console.Write(".");
					}
				}
			}
			Console.WriteLine();

			if (decryptionKey != null || decryptionKeyParams != null) {

				// Decryption key given, check and count votes

				var decryptionQueue = Channel.CreateBounded<VoteBatch>(degreeOfParallelism);
				var countingQueue = Channel.CreateBounded<VoteBatch>(degreeOfParallelism);
				var decryptionTasks = new List<Task>();
				var countingTasks = new List<Task>();

				for (var i = 0; i < degreeOfParallelism; i++) {
					decryptionTasks.Add(Task.Run(() => decryptVotesAsync(decryptionQueue.Reader, countingQueue.Writer)));
					countingTasks.Add(Task.Run(() => checkAndCountVotesAsync(countingQueue.Reader, results)));
				}

				Console.Write("Counting votes ... ");

				await readVotesAsync(votesCsvFile, decryptionQueue.Writer);

				decryptionQueue.Writer.Complete();
				await Task.WhenAll(decryptionTasks);

				countingQueue.Writer.Complete();
				await Task.WhenAll(countingTasks);

				Console.WriteLine($" DONE");
				return results;

			} else {

				// Decryption key not given, only check votes

				var checkingQueue = Channel.CreateBounded<VoteBatch>(degreeOfParallelism);
				var checkingTasks = new List<Task>();

				for (var i = 0; i < degreeOfParallelism; i++) {
					checkingTasks.Add(Task.Run(() => checkVotesAsync(checkingQueue.Reader)));
				}

				Console.Write("Checking votes ... ");

				await readVotesAsync(votesCsvFile, checkingQueue.Writer);

				checkingQueue.Writer.Complete();
				await Task.WhenAll(checkingTasks);

				Console.WriteLine($" DONE");
				return null;
			}
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

		private async Task checkVotesAsync(ChannelReader<VoteBatch> inQueue) {
			await foreach (var batch in inQueue.ReadAllAsync()) {
				foreach (var vote in batch.EncryptedVotes) {
					checkVote(vote);
				}
				Console.Write($"[{batch.Index:D3}V]");
			}
		}

		private async Task checkAndCountVotesAsync(ChannelReader<VoteBatch> inQueue, ElectionResultCollection results) {
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
			var encodedValue = Util.DecodeHex(csvEntry.Value);
			var cmsSignature = Util.DecodeHex(csvEntry.CmsSignature);
			var serverSignature = Util.DecodeHex(csvEntry.ServerSignature);
			var value = VoteEncoding.Decode(encodedValue);
			return new Vote(poolId, slot, encodedValue, cmsSignature, serverSignature, csvEntry.ServerInstanceId, value);
		}

		private async Task<DecryptionTable> decryptChoicesAsync(List<Vote> votes) {
			var ciphers = votes.SelectMany(v => v.Value.Choices.Select(c => c.EncryptedChoice));
			var plaintexts = decryptionKey != null
				? ciphers.Select(c => decryptionKey.Decrypt(c, RSAEncryptionPadding.OaepSHA256))
				: await webVaultClient.DecryptBatchAsync(decryptionKeyParams.KeyId, ciphers);
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

		private RSA getPublicKey(byte[] encodedPublicKey) {
			var rsa = RSA.Create();
			rsa.ImportSubjectPublicKeyInfo(encodedPublicKey, out _);
			return rsa;
		}

		#endregion
	}
}
