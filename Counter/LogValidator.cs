using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
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
	private readonly bool isChained = isChained;
	private Task processingTask;

	public Task Completion => processingTask;

	public void Start() {
		processingTask = Task.Run(processAsync);
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
			previous = record;
		}
	}

	private bool? verify(VotingEventRecord current, VotingEventRecord previous) {

		if (current.ServerSignature == null) {
			return null;
		}

		var currentEncoded = VotingEventEncoding.Encode(current, server.VotingEventSignatureVersion, previous?.ServerSignature);
		if (!Util.VerifyServerSignature(server.PublicKey, currentEncoded, current.ServerSignature)) {
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
