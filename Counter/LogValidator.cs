using Counter.Csv;
using System.Security.Cryptography;
using System.Threading.Channels;
using System.Threading.Tasks;
using Webvoto.VotingSystem.Auditing;

namespace Counter;

public class LogValidator(

	Server server,
	VotingEventValidationResults vr,
	bool isChained

) {

	private readonly Channel<SignedVotingEventRecord> channel = Channel.CreateBounded<SignedVotingEventRecord>(500);

	public Task ProcessingTask { get; private set; }

	public void Start() {
		ProcessingTask = Task.Run(processAsync);
	}

	public async Task EnqueueAsync(SignedVotingEventRecord record) {
		await channel.Writer.WriteAsync(record);
	}

	public void Complete() {
		channel.Writer.Complete();
	}

	private async Task processAsync() {

		SignedVotingEventRecord previous = null;

		await foreach (var record in channel.Reader.ReadAllAsync()) {
			var result = verify(record, previous);
			vr.AddResult(result);
			if (isChained) {
				previous = record;
			}
		}
	}

	private bool? verify(SignedVotingEventRecord current, SignedVotingEventRecord previous) {

		if (current.ServerSignature == null) {
			return null;
		}

		var currentEncoded = VotingEventEncoding.Encode(current, server.VotingEventSignatureVersion, previous?.ServerSignature);
		var serverSigOk = server.PublicKey.VerifyData(currentEncoded, current.ServerSignature, HashAlgorithmName.SHA256);
		if (!serverSigOk) {
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
}
