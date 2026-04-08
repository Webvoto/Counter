using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using static Counter.ServerProvider;
using static Counter.Util;
using static Counter.VotingEventValidator;

namespace Counter;

public class LogValidator {
	private readonly Channel<VotingEventCsvRecord> channel;
	private readonly CheckStats stats;
	private readonly Server server;

	private Task processingTask;

	public Task Completion => processingTask;

	public LogValidator(Server server, CheckStats stats) {
		this.stats = stats;
		this.server = server;

		channel = Channel.CreateBounded<VotingEventCsvRecord>(new BoundedChannelOptions(500) {
			FullMode = BoundedChannelFullMode.Wait
		});
	}

	public void Start() {
		processingTask = Task.Run(processAsync);
	}

	public async Task EnqueueAsync(VotingEventCsvRecord record) {
		await channel.Writer.WriteAsync(record);
	}

	public void Complete() {
		channel.Writer.Complete();
	}

	private async Task processAsync() {
		VotingEventCsvRecord previous = null;
		var expectedSequence = 0;

		await foreach (var record in channel.Reader.ReadAllAsync()) {
			var isChained = !string.IsNullOrEmpty(record.ChainedLogId);
			var expectedSequenceStr = isChained ? expectedSequence.ToString() : null;
			var sigCheckResult = verifySignature(record, previous);
			var sequenceCheck = verifySequence(record, expectedSequenceStr);

			stats.AddResult(sigCheckResult, sequenceCheck);

			previous = record;

			expectedSequence++;
		}
	}

	private bool? verifySignature(VotingEventCsvRecord current, VotingEventCsvRecord previous) {
		byte[] lastEventSignature = null;

		if (!string.IsNullOrEmpty(previous?.ServerSignature)) {
			lastEventSignature = parseSignature(previous.ServerSignature);
		}

		if (string.IsNullOrEmpty(current.ServerSignature)) {
			return null;
		}

		var payload = getSignedFields(current, server.VotingEventSignatureVersion, lastEventSignature);

		var dataBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload));

		var signatureBytes = parseSignature(current.ServerSignature);

		return Util.VerifyServerSignature(server.PublicKey, dataBytes, signatureBytes);
	}

	private bool verifySequence(VotingEventCsvRecord record, string expectedSequence) 
		=> record.Sequence == expectedSequence;

	private static List<string> getSignedFields(VotingEventCsvRecord record, int version, byte[] lastEventSignature = null) => version switch {

		/*
		 * DO NOT CHANGE EXISTING VERSIONS!
		 * 
		 * When a new field is added to VotingEvent, create a new version with a new field list containing the new field.
		 */

		1 => [
			record.Id,
			record.DateUtc.ToString("yyyy-MM-dd HH:mm:ss'Z'"),
			record.TypeCode,
			record.SubscriptionId,
			record.SessionId,
			record.QuestionId,
			record.VoterId,
			record.MemberId,
			record.AgentId,
			record.VotingChannelCode,
			record.RemoteIP,
			record.RemotePort,
			record.AzureRef,
			record.UserAgentString,
			record.IdentifierKindCode,
			record.Identifier,
			record.DelegateVoterId,
			record.VoterOtpId,
			record.BioSessionId,
			record.BioAuthenticationFailureCode,
			record.BioEnrollmentFailureCode,
			record.CertificateTypeCode,
			record.CloudCertificateAuthenticationFailureCode,
			record.AuthServerAuthenticationFailureCode,
			record.WebPkiAuthenticationFailureCode,
			record.OtpCheckFailureCode,
			record.CertificateId,
			record.ValidationResultsBlobId,
			record.VoterContactId,
			record.SubmitVoteFailureCode,
			record.CausedVoterLock,
			],

		2 => [
			record.LogNumber,
			record.Sequence,
			lastEventSignature != null ? Convert.ToBase64String(lastEventSignature) : null,
			.. getSignedFields(record, 1),
			record.WorkerId,
			record.VoteBoxId,
			record.Details,
		],

		3 => [
			record.ChainedLogId,
			.. getSignedFields(record, 2),
			record.PasswordCheckFailureCode,
			record.PasswordId,
			record.CampaignNotificationId,
			],

		4 => [
			.. getSignedFields(record, 3),
			record.VoterAddressId,
		],

		_ => throw new NotImplementedException()
	};

	private static string removeHexPrefix(string value) {
		if (string.IsNullOrWhiteSpace(value)) {
			return value;
		}

		return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value.Substring(2) : value;
	}

	private static byte[] parseSignature(string signature) {
		signature = removeHexPrefix(signature);
		if (isHex(signature)) {
			return Convert.FromHexString(signature);
		}

		return Convert.FromBase64String(signature);
	}

	private static bool isHex(string value)
		=> value.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
}
