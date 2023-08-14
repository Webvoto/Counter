using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Counter {

	public class PartyCsvRecord {

		public string PartyId { get; set; }

		public string PartyName { get; set; }

		public string PartyNumber { get; set; }

		public string ElectionId { get; set; }

		public string ElectionName { get; set; }
	}

	public class PartiesCsvReader : IDisposable {

		private readonly Stream stream;
		private readonly StreamReader streamReader;
		private readonly CsvReader csvReader;

		public static PartiesCsvReader Open(FileInfo file) {
			var stream = file.OpenRead();
			var streamReader = new StreamReader(stream);
			var csvReader = new CsvReader(streamReader, CultureInfo.InvariantCulture);
			return new PartiesCsvReader(stream, streamReader, csvReader);
		}

		private PartiesCsvReader(Stream stream, StreamReader streamReader, CsvReader csvReader) {
			this.stream = stream;
			this.streamReader = streamReader;
			this.csvReader = csvReader;
		}

		public IEnumerable<PartyCsvRecord> GetRecords()
			=> csvReader.GetRecords<PartyCsvRecord>();

		public void Dispose() {
			csvReader.Dispose();
			streamReader.Dispose();
			stream.Dispose();
		}
	}
}
