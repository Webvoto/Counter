using Counter.Csv;
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

		private record Vote(int PoolId, int SlotNumber, byte[] EncodedValue, byte[] CmsSignature, byte[] ServerSignature, int ServerInstanceId, VoteValue Value, string VoteEncryptionPublicKeyThumbprint);

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

		private readonly ServerProvider serverProvider;
		private Dictionary<string, RSA> decryptionKeys = [];
		private X509Certificate2 signatureCertificate;
		private RSA signatureCertificatePublicKey;

		public VoteCounter(ServerProvider serverProvider) {
			this.serverProvider = serverProvider;
		}

		public void Initialize(FileInfo signatureCertificateFile, IEnumerable<FileInfo> decryptionKeyFiles) {
			
			Console.WriteLine("Reading signature certificate ...");
			signatureCertificate = X509CertificateLoader.LoadCertificateFromFile(signatureCertificateFile.FullName);
			signatureCertificatePublicKey = signatureCertificate.GetRSAPublicKey();
			
			if (!decryptionKeyFiles.Any()) {
				Console.WriteLine("WARNING: Decryption keys not found, votes will only be checked!");
			} else {
				Console.WriteLine("Reading decryption keys ...");
				foreach (var decryptionKeyFile in decryptionKeyFiles) {
					loadDecryptionKey(decryptionKeyFile);
				}
			}
		}

		private void loadDecryptionKey(FileInfo decryptionKeyFile) {
			var decryptionKeyPkcs8Pem = File.ReadAllBytes(decryptionKeyFile.FullName);
			var decryptionKeyPkcs8Bytes = Util.DecodePem(decryptionKeyPkcs8Pem);
			
			Console.Write($"Please provide the password for the decryption key '{decryptionKeyFile.Name}': ");
			var password = Console.ReadLine();
			
			var decryptionKey = RSA.Create();
			decryptionKey.ImportEncryptedPkcs8PrivateKey(Encoding.UTF8.GetBytes(password), decryptionKeyPkcs8Bytes, out _);
			var decryptionKeyPublicKeyThumbprint = Convert.ToHexStringLower(SHA256.HashData(decryptionKey.ExportSubjectPublicKeyInfo()));

			decryptionKeys[decryptionKeyPublicKeyThumbprint] = decryptionKey;
		}

		public async Task<ElectionResultCollection> CountAsync(FileInfo votesCsvFile, int degreeOfParallelism) {

			var results = new ElectionResultCollection();

			if (decryptionKeys.Count > 0) {

				// Decryption keys given, check and count votes

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

				// Decryption keys not given, only check votes

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
				batch.DecryptionTable = decryptContentEncryptionKeys(batch.EncryptedVotes);
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
			var slot = csvEntry.SlotNumber;
			var encodedValue = Util.DecodeHex(csvEntry.Value);
			var cmsSignature = Util.DecodeHex(csvEntry.CmsSignature);
			var serverSignature = Util.DecodeHex(csvEntry.ServerSignature);
			var value = VoteEncoding.Decode(encodedValue);
			var voteEncryptionPublicKeyThumbprint = Convert.ToHexStringLower(Util.DecodeHex(csvEntry.VoteEncryptionPublicKeyThumbprint));
			return new Vote(poolId, slot, encodedValue, cmsSignature, serverSignature, csvEntry.ServerInstanceId, value, voteEncryptionPublicKeyThumbprint);
		}

		private DecryptionTable decryptContentEncryptionKeys(List<Vote> votes) {
			var encryptedCeks = votes.ToDictionary(v => v, v => getEncryptedCek(v.Value.EncryptedChoices));
			var decryptedCeks = votes.ToDictionary(
				v => v,
				v => decryptionKeys.TryGetValue(v.VoteEncryptionPublicKeyThumbprint, out var decryptionKey)
					? decryptionKey.Decrypt(encryptedCeks[v], RSAEncryptionPadding.OaepSHA256)
					: throw new Exception($"Vote {v.PoolId}:{v.SlotNumber} is encrypted with unknown key: {v.VoteEncryptionPublicKeyThumbprint}")
				);
			return new DecryptionTable(votes.Select(v => encryptedCeks[v]), votes.Select(v => decryptedCeks[v]));
		}

		private void checkVote(Vote vote) {

			// Check server signature
			var server = serverProvider.GetRequiredServer(vote.ServerInstanceId);

			var serverSigOk = server.PublicKey.VerifyData(vote.CmsSignature, vote.ServerSignature, HashAlgorithmName.SHA256);
			if (!serverSigOk) {
				throw new Exception($"Vote on pool {vote.PoolId} slot {vote.SlotNumber} has an invalid server signature");
			}

			// Check signing certificate signature
			var encodedSignedAttrs = CmsEncoding.EncodeSignedAttributes(SHA256.HashData(vote.EncodedValue), signatureCertificate);
			if (!signatureCertificatePublicKey.VerifyData(encodedSignedAttrs, vote.CmsSignature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)) {
				throw new Exception("Signature mismatch");
			}

			// Check PoolId and Slot integrity
			if (vote.PoolId != vote.Value.PoolId || vote.SlotNumber != vote.Value.SlotNumber) {
				throw new Exception("Vote address corruption");
			}
		}

		private void countVote(ElectionResultCollection results, DecryptionTable decryptionTable, Vote vote) {
			var decryptedChoices = decryptChoices(decryptionTable, vote.Value.EncryptedChoices);
			var decodedChoices = VoteEncoding.DecodeChoices(decryptedChoices);
			foreach (var choice in decodedChoices) {
				results
					.GetOrAddElection(vote.Value.QuestionId.ToString())
					.GetOrAddDistrict(vote.Value.MemberDistrictId.ToString())
					.GetOrAddParty(choice.ToString())
					.Increment();
			}
		}

		private const int EncryptedCekLength = 256; // RSA cryptogram with 2048-bit key
		private const int ContentEncryptionBlockLength = 16; // AES block length

		private byte[] decryptChoices(DecryptionTable decryptionTable, byte[] cryptogram) {

			/*
			 * Terminology:
			 * 
			 * - CEK: content encryption key (AES-256)
			 * - CEIV: content encryption IV (AES, 16 bytes)
			 * - KEK: key encryption key (RSA)
			 * 
			 * Format:
			 * 
			 * 1. CEK encrypted with KEK (2048-bit RSA encryption, 256 bytes)
			 * 2. CEIV (16 bytes)
			 * 3. AES-encrypted content (remaining bytes)
			 */

			// Check length
			var encrytedContentLength = cryptogram.Length - EncryptedCekLength - ContentEncryptionBlockLength;
			if (encrytedContentLength <= 0) {
				throw new Exception($"The given cryptogram has too few bytes: {cryptogram.Length}");
			}
			if (encrytedContentLength % ContentEncryptionBlockLength != 0) {
				throw new Exception($"The given cryptogram has an encrypted content with inconsistent length: {encrytedContentLength}");
			}

			// Parse
			var encryptedCek = getEncryptedCek(cryptogram);
			var ceiv = cryptogram.Skip(EncryptedCekLength).Take(ContentEncryptionBlockLength).ToArray();
			var encryptedContent = cryptogram.Skip(EncryptedCekLength + ContentEncryptionBlockLength).ToArray();

			// Get previously decrypted CEK
			var cek = decryptionTable.GetDecryption(encryptedCek);

			// Decrypt content with CEK
			using var aes = Aes.Create();
			aes.KeySize = 256;
			aes.Mode = CipherMode.CBC;
			aes.Padding = PaddingMode.PKCS7;
			aes.Key = cek;
			aes.IV = ceiv;

			using var decryptor = aes.CreateDecryptor();

			try {
				return decryptor.TransformFinalBlock(encryptedContent, 0, encryptedContent.Length);
			} catch (CryptographicException ex) {
				throw new Exception("The enclosed content cryptogram could not be decrypted", ex);
			}
		}

		private byte[] getEncryptedCek(byte[] cryptogram) => cryptogram.Take(EncryptedCekLength).ToArray();
	}
}
