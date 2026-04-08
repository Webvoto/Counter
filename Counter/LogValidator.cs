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

		await foreach (var record in channel.Reader.ReadAllAsync()) {
			var result = verify(record, previous);

			stats.AddResult(result);

			previous = record;
		}
	}

	private bool? verify(VotingEventCsvRecord current, VotingEventCsvRecord previous) {
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

	private static List<string> getSignedFields(VotingEventCsvRecord record, int version, byte[] lastEventSignature = null) => version switch {

		/*
		 * DO NOT CHANGE EXISTING VERSIONS!
		 * 
		 * When a new field is added to VotingEvent, create a new version with a new field list containing the new field.
		 */

		1 => [
			Normalize(record.Id)?.ToLower(),
			Normalize(record.DateUtc.ToString("yyyy-MM-dd HH:mm:ss'Z'")),
			Normalize(record.TypeCode),
			Normalize(record.SubscriptionId)?.ToLower(),
			Normalize(record.SessionId)?.ToLower(),
			Normalize(record.QuestionId)?.ToLower(),
			Normalize(record.VoterId)?.ToLower(),
			Normalize(record.MemberId)?.ToLower(),
			Normalize(record.AgentId)?.ToLower(),
			Normalize(record.VotingChannelCode),
			Normalize(record.RemoteIP),
			Normalize(record.RemotePort),
			Normalize(record.AzureRef),
			Normalize(record.UserAgentString),
			Normalize(record.IdentifierKindCode),
			Normalize(record.Identifier),
			Normalize(record.DelegateVoterId)?.ToLower(),
			Normalize(record.VoterOtpId)?.ToLower(),
			Normalize(record.BioSessionId)?.ToLower(),
			Normalize(record.BioAuthenticationFailureCode),
			Normalize(record.BioEnrollmentFailureCode),
			Normalize(record.CertificateTypeCode),
			Normalize(record.CloudCertificateAuthenticationFailureCode),
			Normalize(record.AuthServerAuthenticationFailureCode),
			Normalize(record.WebPkiAuthenticationFailureCode),
			Normalize(record.OtpCheckFailureCode),
			Normalize(record.CertificateId)?.ToLower(),
			Normalize(record.ValidationResultsBlobId)?.ToLower(),
			Normalize(record.VoterContactId)?.ToLower(),
			Normalize(record.SubmitVoteFailureCode),
			Normalize(record.CausedVoterLock, StringNormalizations.TreatNullWordsAsBlank | StringNormalizations.CoalesceToEmptyString),
			],

		2 => [
			Normalize(record.LogNumber),
			Normalize(record.Sequence),
			lastEventSignature != null ? Convert.ToBase64String(lastEventSignature) : null,
			.. getSignedFields(record, 1),
			Normalize(record.WorkerId)?.ToLower(),
			Normalize(record.VoteBoxId)?.ToLower(),
			Normalize(record.Details),
		],

		3 => [
			Normalize(record.ChainedLogId)?.ToLower(),
			.. getSignedFields(record, 2),
			Normalize(record.PasswordCheckFailureCode),
			Normalize(record.PasswordId)?.ToLower(),
			Normalize(record.CampaignNotificationId)?.ToLower(),
			],

		4 => [
			.. getSignedFields(record, 3),
			Normalize(record.VoterAddressId)?.ToLower(),
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
