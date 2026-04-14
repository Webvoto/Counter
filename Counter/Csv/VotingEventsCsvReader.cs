using CsvHelper.Configuration.Attributes;
using System;
using System.IO;
using Webvoto.VotingSystem.Auditing;

namespace Counter.Csv;

public class SignedVotingEventRecord : VotingEventRecord {

	public int ServerInstanceId { get; set; }

	public byte[] ServerSignature { get; set; }
}

public class VotingEventCsvRecord {

	public int ServerInstanceId { get; set; }

	// V1
	public string Id { get; set; }

	public DateTime DateUtc { get; set; }

	public string TypeCode { get; set; }

	public string SubscriptionId { get; set; }

	public string SessionId { get; set; }

	public string QuestionId { get; set; }

	public string VoterId { get; set; }

	public string MemberId { get; set; }

	public string AgentId { get; set; }

	public string VotingChannelCode { get; set; }

	public string RemoteIP { get; set; }

	public string RemotePort { get; set; }

	public string AzureRef { get; set; }

	public string UserAgentString { get; set; }

	public string IdentifierKindCode { get; set; }

	public string Identifier { get; set; }

	public string DelegateVoterId { get; set; }

	public string VoterOtpId { get; set; }

	public string BioSessionId { get; set; }

	public string BioAuthenticationFailureCode { get; set; }

	public string BioEnrollmentFailureCode { get; set; }

	public string CertificateTypeCode { get; set; }

	public string CloudCertificateAuthenticationFailureCode { get; set; }

	public string AuthServerAuthenticationFailureCode { get; set; }

	public string WebPkiAuthenticationFailureCode { get; set; }

	public string OtpCheckFailureCode { get; set; }

	public string CertificateId { get; set; }

	public string ValidationResultsBlobId { get; set; }

	public string VoterContactId { get; set; }

	public string SubmitVoteFailureCode { get; set; }

	public string CausedVoterLock { get; set; }

	// V2
	[Optional]
	public string ServerSignature { get; set; }

	[Optional]
	public string LogNumber { get; set; }

	[Optional]
	public string Sequence { get; set; }

	[Optional]
	public string WorkerId { get; set; }

	[Optional]
	public string VoteBoxId { get; set; }

	[Optional]
	public string Details { get; set; }

	// V3
	[Optional]
	public string ChainedLogId { get; set; }

	[Optional]
	public string PasswordCheckFailureCode { get; set; }

	[Optional]
	public string PasswordId { get; set; }

	[Optional]
	public string CampaignNotificationId { get; set; }

	// V4
	[Optional]
	public string VoterAddressId { get; set; }
}

public class VotingEventsCsvReader : CsvReaderBase<VotingEventCsvRecord, SignedVotingEventRecord> {

	private VotingEventsCsvReader(FileInfo file) : base(file) {
	}

	protected override string EnvironmentVariableMoniker => "EVENTS";

	public static VotingEventsCsvReader Open(FileInfo file) {
		var reader = new VotingEventsCsvReader(file);
		reader.Open();
		return reader;
	}

	protected override SignedVotingEventRecord ParseRecord(VotingEventCsvRecord r) => new() {
		Id = ParseGuid(r.Id),
		DateUtc = r.DateUtc,
		TypeCode = ParseString(r.TypeCode),
		ServerInstanceId = r.ServerInstanceId,
		ChainedLogId = ParseNullableGuid(r.ChainedLogId),
		LogNumber = ParseNullableInt(r.LogNumber),
		Sequence = ParseNullableInt(r.Sequence),
		SubscriptionId = ParseNullableGuid(r.SubscriptionId),
		SessionId = ParseNullableGuid(r.SessionId),
		QuestionId = ParseNullableGuid(r.QuestionId),
		VoterId = ParseNullableGuid(r.VoterId),
		MemberId = ParseNullableGuid(r.MemberId),
		AgentId = ParseNullableGuid(r.AgentId),
		WorkerId = ParseNullableGuid(r.WorkerId),
		VoteBoxId = ParseNullableGuid(r.VoteBoxId),
		VotingChannelCode = ParseString(r.VotingChannelCode),
		RemoteIP = ParseString(r.RemoteIP),
		RemotePort = ParseNullableInt(r.RemotePort),
		AzureRef = ParseString(r.AzureRef),
		UserAgentString = ParseString(r.UserAgentString),
		IdentifierKindCode = ParseString(r.IdentifierKindCode),
		Identifier = ParseString(r.Identifier),
		DelegateVoterId = ParseNullableGuid(r.DelegateVoterId),
		VoterOtpId = ParseNullableGuid(r.VoterOtpId),
		BioSessionId = ParseNullableGuid(r.BioSessionId),
		BioAuthenticationFailureCode = ParseString(r.BioAuthenticationFailureCode),
		BioEnrollmentFailureCode = ParseString(r.BioEnrollmentFailureCode),
		CertificateTypeCode = ParseString(r.CertificateTypeCode),
		CloudCertificateAuthenticationFailureCode = ParseString(r.CloudCertificateAuthenticationFailureCode),
		AuthServerAuthenticationFailureCode = ParseString(r.AuthServerAuthenticationFailureCode),
		WebPkiAuthenticationFailureCode = ParseString(r.WebPkiAuthenticationFailureCode),
		OtpCheckFailureCode = ParseString(r.OtpCheckFailureCode),
		CertificateId = ParseNullableGuid(r.CertificateId),
		ValidationResultsBlobId = ParseNullableGuid(r.ValidationResultsBlobId),
		VoterContactId = ParseNullableGuid(r.VoterContactId),
		SubmitVoteFailureCode = ParseString(r.SubmitVoteFailureCode),
		CausedVoterLock = ParseNullableBool(r.CausedVoterLock),
		Details = ParseString(r.Details),
		ServerSignature = ParseBinary(r.ServerSignature),
		PasswordCheckFailureCode = ParseString(r.PasswordCheckFailureCode),
		PasswordId = ParseNullableGuid(r.PasswordId),
		CampaignNotificationId = ParseNullableGuid(r.CampaignNotificationId),
		VoterAddressId = ParseNullableGuid(r.VoterAddressId),
	};
}
