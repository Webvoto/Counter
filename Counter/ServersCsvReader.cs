using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Counter {

	public class ServerCsvRecord {

		public int Id { get; set; }

		public string ModuleName { get; set; }

		public string ModuleVersion { get; set; }

		public int VotingEventSignatureVersion { get; set; }

		public string MachineName { get; set; }

		public string DateStarted { get; set; }

		public string PublicKey { get; set; }
	}

	public class ServersCsvReader : IDisposable {

		private readonly Stream stream;
		private readonly StreamReader streamReader;
		private readonly CsvReader csvReader;

		public static ServersCsvReader Open(FileInfo file) {
			var stream = file.OpenRead();
			var streamReader = new StreamReader(stream);
			var useInvariantCulture = bool.TryParse(Environment.GetEnvironmentVariable("COUNTER_USE_INVARIANT_CULTURE_FOR_SERVERS_CSV"), out var b) && b;
			var csvReader = new CsvReader(streamReader, useInvariantCulture ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture);
			return new ServersCsvReader(stream, streamReader, csvReader);
		}

		private ServersCsvReader(Stream stream, StreamReader streamReader, CsvReader csvReader) {
			this.stream = stream;
			this.streamReader = streamReader;
			this.csvReader = csvReader;
		}

		public IEnumerable<ServerCsvRecord> GetRecords()
			=> csvReader.GetRecords<ServerCsvRecord>();

		public void Dispose() {
			csvReader.Dispose();
			streamReader.Dispose();
			stream.Dispose();
		}
	}
}
