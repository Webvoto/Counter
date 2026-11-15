using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Counter.Csv;

public abstract class CsvReaderBase<TInputRecord, TOutputRecord> : IDisposable {

	private readonly FileInfo file;
	private Stream stream;
	private StreamReader streamReader;
	private CsvReader csvReader;

	protected abstract string EnvironmentVariableMoniker { get; }

	protected CsvReaderBase(FileInfo file) {
		this.file = file;
	}

	protected abstract TOutputRecord ParseRecord(TInputRecord record);

	protected void Open() {
		var useInvariantCulture = bool.TryParse(Environment.GetEnvironmentVariable($"COUNTER_USE_INVARIANT_CULTURE_FOR_{EnvironmentVariableMoniker}_CSV"), out var b) && b;
		stream = file.OpenRead();
		streamReader = new StreamReader(stream);
		csvReader = new CsvReader(streamReader, useInvariantCulture ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture);
	}

	public IEnumerable<TOutputRecord> GetRecords() {
		foreach (var record in csvReader.GetRecords<TInputRecord>()) {
			yield return ParseRecord(record);
		}
	}


	protected Guid ParseGuid(string s) => Guid.Parse(s);

	protected Guid? ParseNullableGuid(string s) => IsNull(s) ? null : Guid.Parse(s);

	protected string ParseString(string s) => IsNull(s) ? null : s;

	protected byte[] ParseBinary(string s) => IsNull(s) ? null : Util.DecodeHex(s);

	protected int? ParseNullableInt(string s) => IsNull(s) ? null : int.Parse(s);

	protected decimal? ParseNullableDecimal(string s) => IsNull(s) ? null : decimal.Parse(s);

	protected bool? ParseNullableBool(string s) => IsNull(s) ? null : s switch {
		"0" => false,
		"1" => true,
		_ => throw new FormatException($"Bad bit value: \"{s}\"")
	};

	protected bool ParseBool(string s) => s switch {
		"0" => false,
		"1" => true,
		_ => throw new FormatException($"Bad bit value: \"{s}\"")
	};

	protected DateTime? ParseNullableDate(string s) => IsNull(s) ? null : DateTime.Parse(s);

	// A calendar date may come from a datetime or datetime2 column, exported with a time part (1990-05-01 00:00:00.000). The time must be
	// midnight: any other time means the column holds an instant, and which calendar date was signed is unknown.
	private static readonly string[] dateOnlyFormats = ["yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss.FFFFFFF"];

	protected DateOnly? ParseNullableDateOnly(string s) {
		if (IsNull(s)) {
			return null;
		}
		var dateTime = DateTime.ParseExact(s, dateOnlyFormats, CultureInfo.InvariantCulture, DateTimeStyles.None);
		if (dateTime.TimeOfDay != TimeSpan.Zero) {
			throw new FormatException($"Date has a time other than midnight: \"{s}\"");
		}
		return DateOnly.FromDateTime(dateTime);
	}

	protected bool IsNull(string s) => s == "NULL" || s == null;

	public void Dispose() {
		csvReader?.Dispose();
		streamReader?.Dispose();
		stream?.Dispose();
	}
}

public abstract class CsvReaderBase<TRecord> : CsvReaderBase<TRecord, TRecord> {
	
	protected CsvReaderBase(FileInfo file) : base(file) {
	}

	protected override TRecord ParseRecord(TRecord record) => record;
}
