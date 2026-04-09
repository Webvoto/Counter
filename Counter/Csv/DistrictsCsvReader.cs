using System.Collections.Generic;
using System.IO;

namespace Counter.Csv;

public class DistrictCsvRecord {

	public string SubscriptionId { get; set; }

	public string SubscriptionName { get; set; }

	public string DistrictId { get; set; }

	public string DistrictName { get; set; }
}

public class DistrictsCsvReader : CsvReaderBase<DistrictCsvRecord> {

	private DistrictsCsvReader(FileInfo file) : base(file) {
	}

	protected override string EnvironmentVariableMoniker => "DISTRICTS";

	public static DistrictsCsvReader Open(FileInfo file) {
		var reader = new DistrictsCsvReader(file);
		reader.Open();
		return reader;
	}

	public static List<DistrictCsvRecord> Read(FileInfo file) {
		using var reader = new DistrictsCsvReader(file);
		reader.Open();
		return [.. reader.GetRecords()];
	}
}
