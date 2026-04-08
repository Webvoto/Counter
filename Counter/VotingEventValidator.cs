using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Counter.Util;

namespace Counter {
	public class VotingEventValidator {
		public class CheckStats {

			public int Checked => passed + failed;

			private int passed;
			public int Passed => passed;

			private int failed;
			public int Failed => failed;

			private int undefined;
			public int Undefined => undefined;

			private int sequence;
			public int Sequence => sequence;

			public int AddPassed() => Interlocked.Increment(ref passed);

			public int AddFailed() => Interlocked.Increment(ref failed);

			public int AddUndefined() => Interlocked.Increment(ref undefined);

			public int AddSequence() => Interlocked.Increment(ref sequence);

			public int AddResult(bool? result, bool sequence) {
				if (sequence) {
					AddSequence();
				}

				if (result.HasValue) {
					if (result.Value) {
						return AddPassed();
					} else {
						return AddFailed();
					}
				} else {
					return AddUndefined();
				}
			}
		}

		private readonly ServerProvider serverProvider;

		public VotingEventValidator(ServerProvider serverProvider) {
			this.serverProvider = serverProvider;
		}

		public async Task ValidateAsync(FileInfo file, int degreeOfParallelism) {
			var stats = new CheckStats();

			var validators = new ConcurrentDictionary<string, LogValidator>();

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

			using var reader = VotingEventsCsvReader.Open(file);

			foreach (var record in reader.GetRecords()) {
				var key = getKey(record);

				var validator = validators.GetOrAdd(key, _ => {
					var server = serverProvider.GetRequiredServer(record.ServerInstanceId);
					var v = new LogValidator(server, stats);
					v.Start();
					return v;
				});

				await validator.EnqueueAsync(record);
			}

			foreach (var validator in validators.Values) {
				validator.Complete();
			}

			await Task.WhenAll(validators.Values.Select(v => v.Completion));

			logResults(stats);

			Console.WriteLine(" DONE");
		}

		private static string getKey(VotingEventCsvRecord e)
			=> $"{e.ServerInstanceId}|{e.ChainedLogId}|{e.LogNumber}";

		private void logResults(CheckStats stats) {

			Console.WriteLine($@"
------------------------------------------------------------
# Voting event integrity check results
------------------------------------------------------------
Checked : {stats.Checked:N0}
Passed  : {stats.Passed:N0}
Failed  : {stats.Failed:N0}
Undefined  : {stats.Undefined:N0}
------------------------------------------------------------
			");
		}
	}


}
