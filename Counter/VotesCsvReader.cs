using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Counter {

	public class VoteCsvRecord {

		public int PoolId { get; set; }

		public int SlotNumber { get; set; }

		public string Value { get; set; }

		public string CmsSignature { get; set; }

		public string ServerSignature { get; set; }

		public int ServerInstanceId { get; set; }

		public string VoteEncryptionPublicKeyThumbprint { get; set; }
	}

	public class VotesCsvReader : IDisposable {

		private readonly Stream stream;
		private readonly StreamReader streamReader;
		private readonly CsvReader csvReader;

		public static VotesCsvReader Open(FileInfo file) {
			var stream = file.OpenRead();
			var streamReader = new StreamReader(stream);
			var useInvariantCulture = bool.TryParse(Environment.GetEnvironmentVariable("COUNTER_USE_INVARIANT_CULTURE_FOR_VOTES_CSV"), out var b) && b;
			var csvReader = new CsvReader(streamReader, useInvariantCulture ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture);
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
