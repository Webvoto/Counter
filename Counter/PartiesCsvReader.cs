using CsvHelper;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Counter {

	public class PartyCsvRecord {

		public string SessionName { get; set; }

		public string QuestionId { get; set; }

		public string QuestionName { get; set; }

		public string Id { get; set; }

		public string Name { get; set; }

		public string Identifier { get; set; }

		public int Enabled { get; set; }

		public bool IsEnabled => Enabled != 0;
	}

	public static class PartiesCsvReader {

		public static List<PartyCsvRecord> Read(FileInfo file) {
			using var stream = file.OpenRead();
			using var streamReader = new StreamReader(stream);
			using var csvReader = new CsvReader(streamReader, CultureInfo.CurrentCulture);
			return csvReader.GetRecords<PartyCsvRecord>().ToList();
		}
	}
}
