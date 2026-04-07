using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using static Counter.ServerProvider;
using static Counter.VotingEventValidator;

namespace Counter;

public class ChainValidator {
	private readonly Channel<VotingEventCsvRecord> channel;
	private readonly CheckStats stats;
	private readonly Server server;

	private Task processingTask;

	public Task Completion => processingTask;

	public ChainValidator(Server server, CheckStats stats) {
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
			bool result;

			if (record.ServerSignature != null) {
				result = verify(record, previous);
			} else {
				result = true;
			}

			stats.AddResult(result);

			previous = record;
		}
	}

	private bool verify(VotingEventCsvRecord current, VotingEventCsvRecord previous) {
		byte[] lastSignature = null;

		if (previous?.ServerSignature != null) {
			lastSignature = parseSignature(previous.ServerSignature);
		}

		return verifySignature(current, lastSignature);
	}

	private bool verifySignature(VotingEventCsvRecord record, byte[] lastEventSignature) {
		if (record.ServerSignature == null)
			return true;

		var payload = getSignedFields(record, 4, lastEventSignature);

		var dataBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload));

		var signatureBytes = parseSignature(record.ServerSignature);

		return Util.VerifyServerSignature(server.PublicKey, dataBytes, signatureBytes);
	}

	private static List<string> getSignedFields(VotingEventCsvRecord record, int version, byte[] lastEventSignature = null) => version switch {

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
				normalize(record.UserAgentString),
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
				.. getSignedFields(record, 1),
				normalize(record.WorkerId)?.ToLower(),
				normalize(record.VoteBoxId)?.ToLower(),
				normalize(record.Details),
			],

		3 => [
			normalize(record.ChainedLogId)?.ToLower(),
				.. getSignedFields(record, 2),
				normalize(record.PasswordCheckFailureCode),
				normalize(record.PasswordId)?.ToLower(),
				normalize(record.CampaignNotificationId)?.ToLower(),
			],

		4 => [
			.. getSignedFields(record, 3),
				normalize(record.VoterAddressId)?.ToLower(),
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
		if (string.IsNullOrWhiteSpace(signature)) {
			return null;
		}

		signature = removeHexPrefix(signature);
		if (isHex(signature)) {
			return Convert.FromHexString(signature);
		}

		return Convert.FromBase64String(signature);
	}

	private static bool isHex(string value)
		=> value.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));


	private static string normalize(string value)
		=> StringUtil.Normalize(value);
}
