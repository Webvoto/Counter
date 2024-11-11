using CsvHelper;
using System;
using System.Collections.Generic;
using System.IO;

namespace Counter {

	public class VoteCsvRecord {
		public string Value { get; set; }

		public string CmsSignature { get; set; }
	}

	public class VotesCsvReader : IDisposable {

		private readonly Stream stream;
		private readonly StreamReader streamReader;
		private readonly CsvReader csvReader;

		public static VotesCsvReader Open(FileInfo file) {
			var stream = file.OpenRead();
			var streamReader = new StreamReader(stream);
			var csvReader = new CsvReader(streamReader, Util.CsvConfiguration);
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
