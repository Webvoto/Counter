using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Counter.Util;

namespace Counter {
	public class VotingEventValidator {
		public class CheckStats {

			public int Checked => passed + failed;

			private int passed;
			public int Passed => passed;

			private int failed;
			public int Failed => failed;

			private int undefined;
			public int Undefined => undefined;

			private int sequence;
			public int Sequence => sequence;

			public int AddPassed() => Interlocked.Increment(ref passed);

			public int AddFailed() => Interlocked.Increment(ref failed);

			public int AddUndefined() => Interlocked.Increment(ref undefined);

			public int AddSequence() => Interlocked.Increment(ref sequence);

			public int AddResult(bool? result, bool sequence) {
				if (sequence) {
					AddSequence();
				}

				if (result.HasValue) {
					if (result.Value) {
						return AddPassed();
					} else {
						return AddFailed();
					}
				} else {
					return AddUndefined();
				}
			}
		}

		private readonly ServerProvider serverProvider;

		public VotingEventValidator(ServerProvider serverProvider) {
			this.serverProvider = serverProvider;
		}

		public async Task ValidateAsync(FileInfo file, int degreeOfParallelism) {
			var stats = new CheckStats();

			var validators = new ConcurrentDictionary<string, LogValidator>();

			Console.WriteLine(@"
[INFO] Voting Event Validation

This process assumes that the input CSV file is ORDERED by:

  - ServerInstanceId
  - ChainedLogId
  - LogNumber
  - Sequence

If the file is not ordered, signature validation may FAIL due to broken chaining.

Recommendation:
  Ensure the data is exported using:
  ORDER BY ServerInstanceId, ChainedLogId, LogNumber, Sequence
			");

			using var reader = VotingEventsCsvReader.Open(file);

			foreach (var record in reader.GetRecords()) {
				var key = getKey(record);

				var validator = validators.GetOrAdd(key, _ => {
					var server = serverProvider.GetRequiredServer(record.ServerInstanceId);
					var v = new LogValidator(server, stats);
					v.Start();
					return v;
				});

				await validator.EnqueueAsync(normalizeRecord(record));
			}

			foreach (var validator in validators.Values) {
				validator.Complete();
			}

			await Task.WhenAll(validators.Values.Select(v => v.Completion));

			logResults(stats);

			Console.WriteLine(" DONE");
		}

		private static string getKey(VotingEventCsvRecord e)
			=> $"{e.ServerInstanceId}|{e.ChainedLogId}|{e.LogNumber}";

		private void logResults(CheckStats stats) {

			Console.WriteLine($@"
------------------------------------------------------------
# Voting event integrity check results
------------------------------------------------------------
Checked : {stats.Checked:N0}
Valid sequences  : {stats.Sequence:N0}
Passed  : {stats.Passed:N0}
Failed  : {stats.Failed:N0}
Undefined  : {stats.Undefined:N0}
------------------------------------------------------------
			");
		}

		static VotingEventCsvRecord normalizeRecord(VotingEventCsvRecord r) {
			return new VotingEventCsvRecord {
				ServerInstanceId = r.ServerInstanceId,

				Id = normalizeGuid(r.Id),
				DateUtc = r.DateUtc,
				TypeCode = Normalize(r.TypeCode),
				SubscriptionId = normalizeGuid(r.SubscriptionId),
				SessionId = normalizeGuid(r.SessionId),
				QuestionId = normalizeGuid(r.QuestionId),
				VoterId = normalizeGuid(r.VoterId),
				MemberId = normalizeGuid(r.MemberId),
				AgentId = normalizeGuid(r.AgentId),
				VotingChannelCode = Normalize(r.VotingChannelCode),
				RemoteIP = Normalize(r.RemoteIP),
				RemotePort = Normalize(r.RemotePort),
				AzureRef = Normalize(r.AzureRef),
				UserAgentString = Normalize(r.UserAgentString),
				IdentifierKindCode = Normalize(r.IdentifierKindCode),
				Identifier = Normalize(r.Identifier),
				DelegateVoterId = normalizeGuid(r.DelegateVoterId),
				VoterOtpId = normalizeGuid(r.VoterOtpId),
				BioSessionId = normalizeGuid(r.BioSessionId),
				BioAuthenticationFailureCode = Normalize(r.BioAuthenticationFailureCode),
				BioEnrollmentFailureCode = Normalize(r.BioEnrollmentFailureCode),
				CertificateTypeCode = Normalize(r.CertificateTypeCode),
				CloudCertificateAuthenticationFailureCode = Normalize(r.CloudCertificateAuthenticationFailureCode),
				AuthServerAuthenticationFailureCode = Normalize(r.AuthServerAuthenticationFailureCode),
				WebPkiAuthenticationFailureCode = Normalize(r.WebPkiAuthenticationFailureCode),
				OtpCheckFailureCode = Normalize(r.OtpCheckFailureCode),
				CertificateId = normalizeGuid(r.CertificateId),
				ValidationResultsBlobId = normalizeGuid(r.ValidationResultsBlobId),
				VoterContactId = normalizeGuid(r.VoterContactId),
				SubmitVoteFailureCode = Normalize(r.SubmitVoteFailureCode),
				CausedVoterLock = Normalize(r.CausedVoterLock, StringNormalizations.TreatNullWordsAsBlank | StringNormalizations.CoalesceToEmptyString),

				ServerSignature = r.ServerSignature,
				LogNumber = Normalize(r.LogNumber),
				Sequence = Normalize(r.Sequence),
				WorkerId = normalizeGuid(r.WorkerId),
				VoteBoxId = normalizeGuid(r.VoteBoxId),
				Details = Normalize(r.Details),

				ChainedLogId = normalizeGuid(r.ChainedLogId),
				PasswordCheckFailureCode = Normalize(r.PasswordCheckFailureCode),
				PasswordId = normalizeGuid(r.PasswordId),
				CampaignNotificationId = normalizeGuid(r.CampaignNotificationId),

				VoterAddressId = normalizeGuid(r.VoterAddressId),
			};
		}

		static string normalizeGuid(string v) => Normalize(v)?.ToLower();

	}
}
