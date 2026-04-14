using Counter.Csv;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Counter;

public class VotingEventValidationResults {

	public int Checked => passed + indeterminate + failed;

	private int passed;
	private int indeterminate;
	private int failed;
	private int votes;

	public int Passed => passed;
	public int Indeterminate => indeterminate;
	public int Failed => failed;
	public int Votes => votes;

	public int AddPassed() => Interlocked.Increment(ref passed);
	public int AddIndefinite() => Interlocked.Increment(ref indeterminate);
	public int AddFailed() => Interlocked.Increment(ref failed);
	public int AddVote() => Interlocked.Increment(ref votes);

	public int AddResult(bool? result) {
		if (!result.HasValue) {
			return AddIndefinite();
		} else if (result.Value) {
			return AddPassed();
		} else {
			return AddFailed();
		}
	}
}

public partial class VotingEventValidator(

	ServerProvider serverProvider

) {
	public async Task ValidateAsync(FileInfo file, int degreeOfParallelism) {

		Console.WriteLine();
		Console.WriteLine($"Checking voting events ...");

		var vr = new VotingEventValidationResults();

		var validators = new ConcurrentDictionary<string, LogValidator>();

		using var reader = VotingEventsCsvReader.Open(file);

		foreach (var ev in reader.GetRecords()) {

			var key = getValidatorKey(ev, out var isChained);

			var validator = validators.GetOrAdd(key, _ => {
				var server = serverProvider.GetRequiredServer(ev.ServerInstanceId);
				var v = new LogValidator(server, vr, isChained);
				v.Start();
				return v;
			});

			await validator.EnqueueAsync(ev);
		}

		foreach (var validator in validators.Values) {
			validator.Complete();
		}

		await Task.WhenAll(validators.Values.Select(v => v.ProcessingTask));

		logResults(vr);

		Console.WriteLine("DONE");
	}

	private static string getValidatorKey(SignedVotingEventRecord e, out bool isChained) {
		isChained = e.ChainedLogId.HasValue || e.LogNumber.HasValue;
		return $"{e.ServerInstanceId}:{e.ChainedLogId}:{e.LogNumber}";
	}

	private void logResults(VotingEventValidationResults vr) {

		Console.WriteLine($@"
------------------------------------------------------------
# Voting event integrity check results
------------------------------------------------------------
Checked       : {vr.Checked:N0}
Passed        : {vr.Passed:N0} ({vr.Votes:N0} votes)
Indeterminate : {vr.Indeterminate:N0}
Failed        : {vr.Failed:N0}
------------------------------------------------------------
			");

		if (vr.Failed > 0 || vr.Indeterminate > 0) {
			Console.WriteLine(@"
[INFO] Voting Event Validation

This process assumes that the input CSV file is ORDERED by:

  - ServerInstanceId
  - ChainedLogId
  - LogNumber
  - Sequence

If the file is not ordered, signature validation may FAIL due to broken chaining.

Recommendation:
  Ensure the data is exported using:
  ORDER BY ServerInstanceId, ChainedLogId, LogNumber, Sequence
			");
		}
	}
}
