using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Counter;

public class LogValidator(

	Server server,
	VotingEventValidationResults vr,
	bool isChained

) {

	private readonly Channel<VotingEvent> channel = Channel.CreateBounded<VotingEvent>(500);
	private readonly bool isChained = isChained;
	private Task processingTask;

	public Task Completion => processingTask;

	public void Start() {
		processingTask = Task.Run(processAsync);
	}

	public async Task EnqueueAsync(VotingEvent record) {
		await channel.Writer.WriteAsync(record);
	}

	public void Complete() {
		channel.Writer.Complete();
	}

	private async Task processAsync() {

		VotingEvent previous = null;

		await foreach (var record in channel.Reader.ReadAllAsync()) {
			var result = verify(record, previous);
			vr.AddResult(result);
			previous = record;
		}
	}

	private bool? verify(VotingEvent current, VotingEvent previous) {

		if (current.ServerSignature == null) {
			return null;
		}

		var fields = getToSignFields(current, server.VotingEventSignatureVersion, previous?.ServerSignature);
		var encodedFields = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(fields));
		if (!Util.VerifyServerSignature(server.PublicKey, encodedFields, current.ServerSignature)) {
			return false;
		}

		if (isChained) {

			if (!current.Sequence.HasValue) {
				return false;
			}

			if (previous != null && !previous.Sequence.HasValue) {
				return null;
			}

			if (current.Sequence.Value != (previous != null ? previous.Sequence.Value + 1 : 0)) {
				return false;
			}
		}

		return true;
	}

	private static List<string> getToSignFields(VotingEvent ve, int version, byte[] lastEventSignature = null) => version switch {

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
			.. getToSignFields(ve, 1),
			ve.WorkerId?.ToString(),
			ve.VoteBoxId?.ToString(),
			ve.Details,
		],

		3 => [
			ve.ChainedLogId?.ToString(),
			.. getToSignFields(ve, 2),
			ve.PasswordCheckFailureCode,
			ve.PasswordId?.ToString(),
			ve.CampaignNotificationId?.ToString(),
		],
		4 => [
			.. getToSignFields(ve, 3),
			ve.VoterAddressId?.ToString(),
		],

		_ => throw new NotImplementedException()
	};
}
