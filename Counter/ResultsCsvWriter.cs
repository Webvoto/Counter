using CsvHelper;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Counter {

	public class ResultCsvRecord {

		public string Election { get; set; }

		public string District { get; set; }

		public string Party { get; set; }

		public int Votes { get; set; }
	}

	public class ResultsCsvWriter {

		public static void Write(ElectionResultCollection results, Stream outStream) {

			var resultRecords = results.ElectionResultsOrdered.SelectMany(
				er => er.DistrictResultsOrdered.SelectMany(
					dr => dr.PartyResultsOrdered.Select(
						pr => new ResultCsvRecord {
							Election = er.DisplayName,
							District = dr.DisplayName,
							Party = pr.DisplayName,
							Votes = pr.Votes,
						}
					)
				)
			);

			using var streamWriter = new StreamWriter(outStream, Encoding.UTF8);
			using var csvWriter = new CsvWriter(streamWriter, Thread.CurrentThread.CurrentCulture);
			csvWriter.WriteRecords(resultRecords);
		}
	}
}
