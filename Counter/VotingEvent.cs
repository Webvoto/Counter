using System;

namespace Counter;

public class VotingEvent {
	public Guid Id { get; set; }
	public DateTime DateUtc { get; set; }
	public string TypeCode { get; set; }
	public int ServerInstanceId { get; set; }
	public Guid? ChainedLogId { get; set; }
	public int? LogNumber { get; set; }
	public int? Sequence { get; set; }
	public Guid? SubscriptionId { get; set; }
	public Guid? SessionId { get; set; }
	public Guid? QuestionId { get; set; }
	public Guid? VoterId { get; set; }
	public Guid? MemberId { get; set; }
	public Guid? AgentId { get; set; }
	public Guid? WorkerId { get; set; }
	public Guid? VoteBoxId { get; set; }
	public string VotingChannelCode { get; set; }
	public string RemoteIP { get; set; }
	public int? RemotePort { get; set; }
	public string AzureRef { get; set; }
	public string UserAgentString { get; set; }
	public string IdentifierKindCode { get; set; }
	public string Identifier { get; set; }
	public Guid? DelegateVoterId { get; set; }
	public Guid? VoterOtpId { get; set; }
	public Guid? BioSessionId { get; set; }
	public string BioAuthenticationFailureCode { get; set; }
	public string BioEnrollmentFailureCode { get; set; }
	public string CertificateTypeCode { get; set; }
	public string CloudCertificateAuthenticationFailureCode { get; set; }
	public string AuthServerAuthenticationFailureCode { get; set; }
	public string WebPkiAuthenticationFailureCode { get; set; }
	public string OtpCheckFailureCode { get; set; }
	public Guid? CertificateId { get; set; }
	public Guid? ValidationResultsBlobId { get; set; }
	public Guid? VoterContactId { get; set; }
	public string SubmitVoteFailureCode { get; set; }
	public bool? CausedVoterLock { get; set; }
	public string Details { get; set; }
	public byte[] ServerSignature { get; set; }
	public string PasswordCheckFailureCode { get; set; }
	public Guid? PasswordId { get; set; }
	public Guid? CampaignNotificationId { get; set; }
	public Guid? VoterAddressId { get; set; }
}
