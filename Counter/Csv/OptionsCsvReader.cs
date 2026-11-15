using CsvHelper.Configuration.Attributes;
using System.Collections.Generic;
using System.IO;
using Webvoto.VotingSystem.Auditing;

namespace Counter.Csv;

public class SignedOptionRecord : OptionRecord {

	public string SessionName { get; set; }

	public string QuestionName { get; set; }

	public int? ServerInstanceId { get; set; }

	public byte[] ServerSignature { get; set; }
}

public class OptionCsvRecord {

	public string SessionName { get; set; }

	public string QuestionId { get; set; }

	public string QuestionName { get; set; }

	public string Id { get; set; }

	public string Name { get; set; }

	public string TypeCode { get; set; }

	public int Order { get; set; }

	public string IsEnabled { get; set; }

	public string ImageBlobId { get; set; }

	public string ImageContentType { get; set; }

	public string ImageThumbprint { get; set; }

	public string Description { get; set; }

	public string DateDeletedUtc { get; set; }

	public string Identifier { get; set; }

	public string ServerInstanceId { get; set; }

	public string ServerSignature { get; set; }

	#region V2

	[Optional]
	public string InscriptionNumber { get; set; }

	[Optional]
	public string IsParent { get; set; }

	[Optional]
	public string ParentOptionId { get; set; }

	[Optional]
	public string RoundStatement { get; set; }

	[Optional]
	public string BirthDate { get; set; }

	[Optional]
	public string InscriptionDate { get; set; }

	[Optional]
	public string TieBreakPriority { get; set; }

	#endregion
}

public class OptionsCsvReader : CsvReaderBase<OptionCsvRecord, SignedOptionRecord> {

	private OptionsCsvReader(FileInfo file) : base(file) {
	}

	protected override string EnvironmentVariableMoniker => "OPTIONS";

	public static OptionsCsvReader Open(FileInfo file) {
		var reader = new OptionsCsvReader(file);
		reader.Open();
		return reader;
	}

	public static List<SignedOptionRecord> Read(FileInfo file) {
		using var reader = new OptionsCsvReader(file);
		reader.Open();
		return [.. reader.GetRecords()];
	}

	protected override SignedOptionRecord ParseRecord(OptionCsvRecord r) => new() {
		Id = ParseGuid(r.Id),
		QuestionId = ParseGuid(r.QuestionId),
		Name = ParseString(r.Name),
		Identifier = ParseString(r.Identifier),
		TypeCode = ParseString(r.TypeCode),
		Order = r.Order,
		IsEnabled = ParseBool(r.IsEnabled),
		ImageContentType = ParseString(r.ImageContentType),
		ImageThumbprint = ParseString(r.ImageThumbprint),
		Description = ParseString(r.Description),
		DateDeletedUtc = ParseNullableDate(r.DateDeletedUtc),
		InscriptionNumber = ParseString(r.InscriptionNumber),
		// The column is missing from older exports, which predate Parent options. When present it is a non-nullable bit, so a NULL is a bad export.
		IsParent = r.IsParent != null && ParseBool(r.IsParent),
		ParentOptionId = ParseNullableGuid(r.ParentOptionId),
		RoundStatement = ParseString(r.RoundStatement),
		BirthDate = ParseNullableDateOnly(r.BirthDate),
		InscriptionDate = ParseNullableDateOnly(r.InscriptionDate),
		TieBreakPriority = ParseNullableInt(r.TieBreakPriority),
		SessionName = r.SessionName,
		QuestionName = r.QuestionName,
		ServerInstanceId = ParseNullableInt(r.ServerInstanceId),
		ServerSignature = ParseBinary(r.ServerSignature),
	};
}
