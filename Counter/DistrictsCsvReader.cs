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

		public string SubscriptionName { get; set; }

		public string DistrictId { get; set; }

		public string DistrictName { get; set; }
	}

	public static class DistrictsCsvReader {

		public static List<DistrictCsvRecord> Read(FileInfo file) {
			using var stream = file.OpenRead();
			using var streamReader = new StreamReader(stream);
			using var csvReader = new CsvReader(streamReader, Thread.CurrentThread.CurrentCulture);
			return csvReader.GetRecords<DistrictCsvRecord>().ToList();
		}
	}
}
