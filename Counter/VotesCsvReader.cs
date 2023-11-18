using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Counter {

	public class VoteCsvRecord {

		public int PoolId { get; set; }

		public int Slot { get; set; }

		public string Value { get; set; }

		public string CmsSignature { get; set; }

		public string ServerSignature { get; set; }

		public int ServerInstanceId { get; set; }

		public string ServerPublicKey { get; set; }
	}

	public class VotesCsvReader : IDisposable {

		private readonly Stream stream;
		private readonly StreamReader streamReader;
		private readonly CsvReader csvReader;

		public static VotesCsvReader Open(FileInfo file) {
			var stream = file.OpenRead();
			var streamReader = new StreamReader(stream);
			var csvReader = new CsvReader(streamReader, CultureInfo.InvariantCulture /* default in SSMS is exporting with commas regardless of the OS culture */);
			return new VotesCsvReader(stream, streamReader, csvReader);
		}

		private VotesCsvReader(Stream stream, StreamReader streamReader, CsvReader csvReader) {
			this.stream = stream;
			this.streamReader = streamReader;
			this.csvReader = csvReader;
		}

		public IEnumerable<VoteCsvRecord> GetRecords()
			=> csvReader.GetRecords<VoteCsvRecord>();

		public void Dispose() {
			csvReader.Dispose();
			streamReader.Dispose();
			stream.Dispose();
		}
	}
}
