using CsvHelper;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Counter {

	public class ResultCsvRecord {

		public string ElectionName { get; set; }

		public string PartyName { get; set; }

		public int? PartyNumber { get; set; }

		public int Votes { get; set; }
	}

	public class ResultsCsvWriter {

		public static void Write(ElectionResultCollection results, Stream outStream) {

			var resultRecords = results.ElectionResultsOrdered.SelectMany(er => er.PartyResultsOrdered.Select(pr => new ResultCsvRecord {
				ElectionName = er.DisplayName,
				PartyName = pr.DisplayName,
				Votes = pr.Votes,
			}));

			using var streamWriter = new StreamWriter(outStream, Encoding.UTF8);
			using var csvWriter = new CsvWriter(streamWriter, Thread.CurrentThread.CurrentCulture);
			csvWriter.WriteRecords(resultRecords);
		}
	}
}
