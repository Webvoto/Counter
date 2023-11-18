using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Counter {

	public class DistrictCsvRecord {

		public string SubscriptionId { get; set; }

		public string DistrictId { get; set; }

		public string DistrictName { get; set; }
	}

	public class DistrictsCsvReader : IDisposable {

		private readonly Stream stream;
		private readonly StreamReader streamReader;
		private readonly CsvReader csvReader;

		public static DistrictsCsvReader Open(FileInfo file) {
			var stream = file.OpenRead();
			var streamReader = new StreamReader(stream);
			var csvReader = new CsvReader(streamReader, Thread.CurrentThread.CurrentCulture);
			return new DistrictsCsvReader(stream, streamReader, csvReader);
		}

		private DistrictsCsvReader(Stream stream, StreamReader streamReader, CsvReader csvReader) {
			this.stream = stream;
			this.streamReader = streamReader;
			this.csvReader = csvReader;
		}

		public IEnumerable<DistrictCsvRecord> GetRecords()
			=> csvReader.GetRecords<DistrictCsvRecord>();

		public void Dispose() {
			csvReader.Dispose();
			streamReader.Dispose();
			stream.Dispose();
		}
	}
}
