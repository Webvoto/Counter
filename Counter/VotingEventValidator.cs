using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Webvoto.VotingSystem.Auditing;

namespace Counter;

public class VotingEventValidationResults {

	public int Checked => passed + indeterminate + failed;

	private int passed;
	private int indeterminate;
	private int failed;

	public int Passed => passed;
	public int Indeterminate => indeterminate;
	public int Failed => failed;

	public int AddPassed() => Interlocked.Increment(ref passed);
	public int AddIndefinite() => Interlocked.Increment(ref indeterminate);
	public int AddFailed() => Interlocked.Increment(ref failed);

	public int AddResult(bool? result) {
		if (!result.HasValue) {
			return AddIndefinite();
		} else if (result.Value) {
			return AddPassed();
		} else {
			return AddFailed();
		}
	}
}

public partial class VotingEventValidator(

	ServerProvider serverProvider

) {
	public async Task ValidateAsync(FileInfo file, int degreeOfParallelism) {

		var vr = new VotingEventValidationResults();

		var validators = new ConcurrentDictionary<string, LogValidator>();

		using var reader = VotingEventsCsvReader.Open(file);

		foreach (var record in reader.GetRecords()) {

			var ev = parseRecord(record);

			var key = getValidatorKey(ev, out var isChained);

			var validator = validators.GetOrAdd(key, _ => {
				var server = serverProvider.GetRequiredServer(record.ServerInstanceId);
				var v = new LogValidator(server, vr, isChained);
				v.Start();
				return v;
			});

			await validator.EnqueueAsync(ev);
		}

		foreach (var validator in validators.Values) {
			validator.Complete();
		}

		await Task.WhenAll(validators.Values.Select(v => v.ProcessingTask));

		logResults(vr);

		Console.WriteLine(" DONE");
	}

	private static string getValidatorKey(VotingEventRecord e, out bool isChained) {
		if (e.ChainedLogId.HasValue) {
			isChained = true;
			return $"{e.ServerInstanceId}:{e.ChainedLogId}:{e.LogNumber}";
		} else {
			isChained = false;
			return $"{e.ServerInstanceId}";
		}
	}

	private void logResults(VotingEventValidationResults vr) {

		Console.WriteLine($@"
------------------------------------------------------------
# Voting event integrity check results
------------------------------------------------------------
Checked       : {vr.Checked:N0}
Passed        : {vr.Passed:N0}
Indeterminate : {vr.Indeterminate:N0}
Failed        : {vr.Failed:N0}
------------------------------------------------------------
			");

		if (vr.Failed > 0 || vr.Indeterminate > 0) {
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
		}
	}

	static VotingEventRecord parseRecord(VotingEventCsvRecord r) => new() {
		Id = parseGuid(r.Id),
		DateUtc = r.DateUtc,
		TypeCode = parseString(r.TypeCode),
		ServerInstanceId = r.ServerInstanceId,
		ChainedLogId = parseNullableGuid(r.ChainedLogId),
		LogNumber = parseNullableInt(r.LogNumber),
		Sequence = parseNullableInt(r.Sequence),
		SubscriptionId = parseNullableGuid(r.SubscriptionId),
		SessionId = parseNullableGuid(r.SessionId),
		QuestionId = parseNullableGuid(r.QuestionId),
		VoterId = parseNullableGuid(r.VoterId),
		MemberId = parseNullableGuid(r.MemberId),
		AgentId = parseNullableGuid(r.AgentId),
		WorkerId = parseNullableGuid(r.WorkerId),
		VoteBoxId = parseNullableGuid(r.VoteBoxId),
		VotingChannelCode = parseString(r.VotingChannelCode),
		RemoteIP = parseString(r.RemoteIP),
		RemotePort = parseNullableInt(r.RemotePort),
		AzureRef = parseString(r.AzureRef),
		UserAgentString = parseString(r.UserAgentString),
		IdentifierKindCode = parseString(r.IdentifierKindCode),
		Identifier = parseString(r.Identifier),
		DelegateVoterId = parseNullableGuid(r.DelegateVoterId),
		VoterOtpId = parseNullableGuid(r.VoterOtpId),
		BioSessionId = parseNullableGuid(r.BioSessionId),
		BioAuthenticationFailureCode = parseString(r.BioAuthenticationFailureCode),
		BioEnrollmentFailureCode = parseString(r.BioEnrollmentFailureCode),
		CertificateTypeCode = parseString(r.CertificateTypeCode),
		CloudCertificateAuthenticationFailureCode = parseString(r.CloudCertificateAuthenticationFailureCode),
		AuthServerAuthenticationFailureCode = parseString(r.AuthServerAuthenticationFailureCode),
		WebPkiAuthenticationFailureCode = parseString(r.WebPkiAuthenticationFailureCode),
		OtpCheckFailureCode = parseString(r.OtpCheckFailureCode),
		CertificateId = parseNullableGuid(r.CertificateId),
		ValidationResultsBlobId = parseNullableGuid(r.ValidationResultsBlobId),
		VoterContactId = parseNullableGuid(r.VoterContactId),
		SubmitVoteFailureCode = parseString(r.SubmitVoteFailureCode),
		CausedVoterLock = parseNullableBool(r.CausedVoterLock),
		Details = parseString(r.Details),
		ServerSignature = parseBinary(r.ServerSignature),
		PasswordCheckFailureCode = parseString(r.PasswordCheckFailureCode),
		PasswordId = parseNullableGuid(r.PasswordId),
		CampaignNotificationId = parseNullableGuid(r.CampaignNotificationId),
		VoterAddressId = parseNullableGuid(r.VoterAddressId),
	};

	private static Guid parseGuid(string s) => Guid.Parse(s);

	private static Guid? parseNullableGuid(string s) => isNull(s) ? null : Guid.Parse(s);

	private static string parseString(string s) => isNull(s) ? null : s;

	private static byte[] parseBinary(string s) => isNull(s) ? null : Util.DecodeHex(s);

	private static int? parseNullableInt(string s) => isNull(s) ? null : int.Parse(s);

	private static bool? parseNullableBool(string s) => isNull(s) ? null : s switch {
		"0" => false,
		"1" => true,
		_ => throw new FormatException($"Bad bit value: \"s\"")
	};

	private static bool isNull(string s) => s == "NULL";
}
