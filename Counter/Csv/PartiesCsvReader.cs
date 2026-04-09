using System.Collections.Generic;
using System.IO;

namespace Counter.Csv;

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

public class PartiesCsvReader : CsvReaderBase<PartyCsvRecord> {
	
	private PartiesCsvReader(FileInfo file) : base(file) {
	}

	protected override string EnvironmentVariableMoniker => "PARTIES";

	public static PartiesCsvReader Open(FileInfo file) {
		var reader = new PartiesCsvReader(file);
		reader.Open();
		return reader;
	}

	public static List<PartyCsvRecord> Read(FileInfo file) {
		using var reader = new PartiesCsvReader(file);
		reader.Open();
		return [.. reader.GetRecords()];
	}
}
