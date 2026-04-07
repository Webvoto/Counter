using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Counter {
	public class VotingEventValidator {
		private class CheckStats {

			public int Checked => passed + failed;

			private int passed;
			public int Passed => passed;

			private int failed;
			public int Failed => failed;

			public int AddPassed() =>  Interlocked.Increment(ref passed);

			public int AddFailed() => Interlocked.Increment(ref failed);

			public int AddResult(bool result) => result ? AddPassed() : AddFailed();
		}

		private class EventBatch {

			public int Index { get; }

			public List<VotingEventCsvRecord> Events { get; }

			public EventBatch(int index, IEnumerable<VotingEventCsvRecord> events) {
				Index = index;
				Events = new List<VotingEventCsvRecord>(events);
			}
		}

		private readonly ServerProvider serverProvider;

		public VotingEventValidator(ServerProvider serverProvider) {
			this.serverProvider = serverProvider;
		}

		public async Task ValidateAsync(FileInfo eventsCsvFile, int degreeOfParallelism) {
			var stats = new CheckStats();
			var verificationQueue = Channel.CreateBounded<EventBatch>(degreeOfParallelism);
			var verificationTasks = new List<Task>();

			for (var i = 0; i < degreeOfParallelism; i++) {
				verificationTasks.Add(Task.Run(() => checkBatchAsync(verificationQueue.Reader, stats)));
			}

			Console.Write("Validating voting events...");

			await readEventsAsync(eventsCsvFile, verificationQueue.Writer);

			verificationQueue.Writer.Complete();
			await Task.WhenAll(verificationTasks);

			Console.WriteLine(" DONE");

			logResults(stats);
		}

		private async Task checkBatchAsync(ChannelReader<EventBatch> reader, CheckStats stats) {
			await foreach (var batch in reader.ReadAllAsync()) {
				VotingEventCsvRecord previous = null;

				foreach (var current in batch.Events) {
					var result = verifyEvent(current, previous);
					stats.AddResult(result);
					previous = current;
				}
			}
		}

		private bool verifyEvent(VotingEventCsvRecord current, VotingEventCsvRecord previous) {
			if (current.ServerSignature == null)
				return true;

			var lastEventSignature =
				!string.IsNullOrEmpty(StringUtil.Normalize(current.ChainedLogId)) &&
				!string.IsNullOrEmpty(previous?.ServerSignature)
					? Convert.FromBase64String(previous.ServerSignature)
					: null;

			return verifySignature(current, lastEventSignature);
		}

		private async Task readEventsAsync(FileInfo file, ChannelWriter<EventBatch> outQueue) {
			var allEvents = new List<VotingEventCsvRecord>();

			using var reader = VotingEventsCsvReader.Open(file);

			foreach (var record in reader.GetRecords()) {
				allEvents.Add(record);
			}

			var grouped = allEvents.GroupBy(e => new {
				e.ServerInstanceId,
				e.LogNumber,
				e.ChainedLogId
			});

			var batchIndex = 0;

			foreach (var group in grouped) {
				var ordered = group
					.OrderBy(e => int.TryParse(e.Sequence, out var seq) ? seq : int.MaxValue)
					.ToList();

				var batch = new EventBatch(batchIndex++, ordered);

				Console.Write($"[{batch.Index:D3}G]");
				await outQueue.WriteAsync(batch);
			}
		}

		private bool verifySignature(VotingEventCsvRecord record, byte[] lastEventSignature) {
			if (record.ServerSignature == null) {
				return true;
			}

			if (!serverPublicKeys.TryGetValue(record.ServerInstanceId, out var publicKey)) {
				return false;
			}

			var payload = getToSignFields(record, votingEventSignatureVersion, lastEventSignature);
			var dataBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload));
			var hex = record.ServerSignature.StartsWith("0x") ? record.ServerSignature.Substring(2) : record.ServerSignature;
			var signatureBytes = Convert.FromHexString(hex);

			return Util.VerifyServerSignature(publicKey, dataBytes, signatureBytes);
		}

		private static List<string> getToSignFields(VotingEventCsvRecord record, int version, byte[] lastEventSignature = null) => version switch {

			/*
			 * DO NOT CHANGE EXISTING VERSIONS!
			 * 
			 * When a new field is added to VotingEvent, create a new version with a new field list containing the new field and update Constants.VotingEventSignatureVersion accordingly
			 */

			1 => [
				normalize(record.Id)?.ToLower(),
				normalize(record.DateUtc.ToString("yyyy-MM-dd HH:mm:ss'Z'")),
				normalize(record.TypeCode),
				normalize(record.SubscriptionId)?.ToLower(),
				normalize(record.SessionId)?.ToLower(),
				normalize(record.QuestionId)?.ToLower(),
				normalize(record.VoterId)?.ToLower(),
				normalize(record.MemberId)?.ToLower(),
				normalize(record.AgentId)?.ToLower(),
				normalize(record.VotingChannelCode),
				normalize(record.RemoteIP),
				normalize(record.RemotePort),
				normalize(record.AzureRef),
				StringUtil.Normalize(record.UserAgentString, StringUtil.CsvStringNormalizationsNullable | StringNormalizations.ReplaceSemicolon),
				normalize(record.IdentifierKindCode),
				normalize(record.Identifier),
				normalize(record.DelegateVoterId)?.ToLower(),
				normalize(record.VoterOtpId)?.ToLower(),
				normalize(record.BioSessionId)?.ToLower(),
				normalize(record.BioAuthenticationFailureCode),
				normalize(record.BioEnrollmentFailureCode),
				normalize(record.CertificateTypeCode),
				normalize(record.CloudCertificateAuthenticationFailureCode),
				normalize(record.AuthServerAuthenticationFailureCode),
				normalize(record.WebPkiAuthenticationFailureCode),
				normalize(record.OtpCheckFailureCode),
				normalize(record.CertificateId)?.ToLower(),
				normalize(record.ValidationResultsBlobId)?.ToLower(),
				normalize(record.VoterContactId)?.ToLower(),
				normalize(record.SubmitVoteFailureCode),
				StringUtil.Normalize(record.CausedVoterLock, StringUtil.CsvStringNormalizationsRequired),
			],

			2 => [
				normalize(record.LogNumber),
				normalize(record.Sequence),
				lastEventSignature != null ? Convert.ToBase64String(lastEventSignature) : null,
				.. getToSignFields(record, 1),
				normalize(record.WorkerId)?.ToLower(),
				normalize(record.VoteBoxId)?.ToLower(),
				normalize(record.Details),
			],

			3 => [
				normalize(record.ChainedLogId),
				.. getToSignFields(record, 2),
				normalize(record.PasswordCheckFailureCode),
				normalize(record.PasswordId)?.ToLower(),
				normalize(record.CampaignNotificationId)?.ToLower(),
			],

			4 => [
				.. getToSignFields(record, 3),
				normalize(record.VoterAddressId)?.ToLower(),
			],

			_ => throw new NotImplementedException()
		};

		private static string normalize(string value) => StringUtil.Normalize(value);

		private void logResults(CheckStats stats) {

			Console.WriteLine($@"
				------------------------------------------------------------
				# Voting event integrity check results
				------------------------------------------------------------
				Checked : {stats.Checked:N0}
				Passed  : {stats.Passed:N0}
				Failed  : {stats.Failed:N0}
				------------------------------------------------------------
			");
		}
	}


}
