using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Webvoto.VotingSystem.Auditing;

public static class VotingEventEncoding {

	public static readonly int LatestVersion = 4; // itentionally not a const!

	public static byte[] Encode(VotingEventRecord ve, int version, byte[] lastEventSignature = null)
		=> Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(getFields(ve, version, lastEventSignature)));

	private static List<string> getFields(VotingEventRecord ve, int version, byte[] lastEventSignature = null) => version switch {

		/*
		 * DO NOT CHANGE EXISTING VERSIONS!
		 * 
		 * When a new field is added to VotingEventRecord, create a new version with a new field list containing the new field and update the LastestVersion above
		 */

		1 => [
			ve.Id.ToString(),
			ve.DateUtc.ToString("u"),
			ve.TypeCode,
			ve.SubscriptionId?.ToString(),
			ve.SessionId?.ToString(),
			ve.QuestionId?.ToString(),
			ve.VoterId?.ToString(),
			ve.MemberId?.ToString(),
			ve.AgentId?.ToString(),
			ve.VotingChannelCode,
			ve.RemoteIP,
			ve.RemotePort?.ToString(),
			ve.AzureRef,
			ve.UserAgentString,
			ve.IdentifierKindCode,
			ve.Identifier,
			ve.DelegateVoterId?.ToString(),
			ve.VoterOtpId?.ToString(),
			ve.BioSessionId?.ToString(),
			ve.BioAuthenticationFailureCode,
			ve.BioEnrollmentFailureCode,
			ve.CertificateTypeCode,
			ve.CloudCertificateAuthenticationFailureCode,
			ve.AuthServerAuthenticationFailureCode,
			ve.WebPkiAuthenticationFailureCode,
			ve.OtpCheckFailureCode,
			ve.CertificateId?.ToString(),
			ve.ValidationResultsBlobId?.ToString(),
			ve.VoterContactId?.ToString(),
			ve.SubmitVoteFailureCode,
			ve.CausedVoterLock.ToString(),
		],

		2 => [
			ve.LogNumber?.ToString(),
			ve.Sequence?.ToString(),
			lastEventSignature != null ? Convert.ToBase64String(lastEventSignature) : null,
			.. getFields(ve, 1),
			ve.WorkerId?.ToString(),
			ve.VoteBoxId?.ToString(),
			ve.Details,
		],

		3 => [
			ve.ChainedLogId?.ToString(),
			.. getFields(ve, 2),
			ve.PasswordCheckFailureCode,
			ve.PasswordId?.ToString(),
			ve.CampaignNotificationId?.ToString(),
		],

		4 => [
			.. getFields(ve, 3),
			ve.VoterAddressId?.ToString(),
		],

		_ => throw new NotImplementedException()
	};
}
