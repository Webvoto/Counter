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

	private readonly Channel<VotingEventRecord> channel = Channel.CreateBounded<VotingEventRecord>(500);

	public Task ProcessingTask { get; private set; }

	public void Start() {
		ProcessingTask = Task.Run(processAsync);
	}

	public async Task EnqueueAsync(VotingEventRecord record) {
		await channel.Writer.WriteAsync(record);
	}

	public void Complete() {
		channel.Writer.Complete();
	}

	private async Task processAsync() {

		VotingEventRecord previous = null;

		await foreach (var record in channel.Reader.ReadAllAsync()) {
			var result = verify(record, previous);
			vr.AddResult(result);
			if (isChained) {
				previous = record;
			}
		}
	}

	private bool? verify(VotingEventRecord current, VotingEventRecord previous) {

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
