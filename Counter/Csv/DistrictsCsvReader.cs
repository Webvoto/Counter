using System;
using System.Collections.Generic;
using System.IO;

namespace Counter.Csv;

public class DistrictRecord {

	public Guid SubscriptionId { get; set; }

	public string SubscriptionName { get; set; }

	public Guid DistrictId { get; set; }

	public string DistrictName { get; set; }
}

public class DistrictCsvRecord {

	public string SubscriptionId { get; set; }

	public string SubscriptionName { get; set; }

	public string DistrictId { get; set; }

	public string DistrictName { get; set; }
}

public class DistrictsCsvReader : CsvReaderBase<DistrictCsvRecord, DistrictRecord> {

	private DistrictsCsvReader(FileInfo file) : base(file) {
	}

	protected override string EnvironmentVariableMoniker => "DISTRICTS";

	protected override DistrictRecord ParseRecord(DistrictCsvRecord r) => new() {
		SubscriptionId = ParseGuid(r.SubscriptionId),
		SubscriptionName = ParseString(r.SubscriptionName),
		DistrictId = ParseGuid(r.DistrictId),
		DistrictName = ParseString(r.DistrictName),
	};

	public static DistrictsCsvReader Open(FileInfo file) {
		var reader = new DistrictsCsvReader(file);
		reader.Open();
		return reader;
	}

	public static List<DistrictRecord> Read(FileInfo file) {
		using var reader = new DistrictsCsvReader(file);
		reader.Open();
		return [.. reader.GetRecords()];
	}
}
