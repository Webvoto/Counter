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

	public class PartyCsvRecord {

		public string SubscriptionId { get; set; }

		public string SubscriptionName { get; set; }

		public string ElectionId { get; set; }

		public string ElectionName { get; set; }

		public string PartyId { get; set; }

		public string PartyName { get; set; }

		public string PartyNumber { get; set; }

		public int Enabled { get; set; }

		public bool IsEnabled => Enabled != 0;
	}

	public static class PartiesCsvReader {

		public static List<PartyCsvRecord> Read(FileInfo file) {
			using var stream = file.OpenRead();
			using var streamReader = new StreamReader(stream);
			using var csvReader = new CsvReader(streamReader, Thread.CurrentThread.CurrentCulture);
			return csvReader.GetRecords<PartyCsvRecord>().ToList();
		}
	}
}
