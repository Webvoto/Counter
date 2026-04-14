using System.IO;

namespace Counter.Csv;

public class VoteCsvRecord {

	public int PoolId { get; set; }

	public int SlotNumber { get; set; }

	public string Value { get; set; }

	public string CmsSignature { get; set; }

	public string ServerSignature { get; set; }

	public int ServerInstanceId { get; set; }

	public string VoteEncryptionPublicKeyThumbprint { get; set; }
}

public class VotesCsvReader : CsvReaderBase<VoteCsvRecord> {

	private VotesCsvReader(FileInfo file) : base(file) {
	}

	protected override string EnvironmentVariableMoniker => "VOTES";

	public static VotesCsvReader Open(FileInfo file) {
		var reader = new VotesCsvReader(file);
		reader.Open();
		return reader;
	}
}
