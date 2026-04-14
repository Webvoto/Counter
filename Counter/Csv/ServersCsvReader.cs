using System.Collections.Generic;
using System.IO;

namespace Counter.Csv;

public class ServerCsvRecord {

	public int Id { get; set; }

	public string ModuleName { get; set; }

	public string ModuleVersion { get; set; }

	public int VotingEventSignatureVersion { get; set; }

	public int OptionSignatureVersion { get; set; }

	public string MachineName { get; set; }

	public string DateStarted { get; set; }

	public string PublicKey { get; set; }
}

public class ServersCsvReader : CsvReaderBase<ServerCsvRecord> {

	private ServersCsvReader(FileInfo file) : base(file) {
	}

	protected override string EnvironmentVariableMoniker => "SERVERS";

	public static ServersCsvReader Open(FileInfo file) {
		var reader = new ServersCsvReader(file);
		reader.Open();
		return reader;
	}

	public static List<ServerCsvRecord> Read(FileInfo file) {
		using var reader = new ServersCsvReader(file);
		reader.Open();
		return [.. reader.GetRecords()];
	}
}
