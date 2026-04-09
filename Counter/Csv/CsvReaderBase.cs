using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Counter.Csv;

public abstract class CsvReaderBase<TRecord> : IDisposable {

	private readonly FileInfo file;
	private Stream stream;
	private StreamReader streamReader;
	private CsvReader csvReader;

	protected abstract string EnvironmentVariableMoniker { get; }

	protected CsvReaderBase(FileInfo file) {
		this.file = file;
	}

	protected void Open() {
		var useInvariantCulture = bool.TryParse(Environment.GetEnvironmentVariable($"COUNTER_USE_INVARIANT_CULTURE_FOR_{EnvironmentVariableMoniker}_CSV"), out var b) && b;
		stream = file.OpenRead();
		streamReader = new StreamReader(stream);
		csvReader = new CsvReader(streamReader, useInvariantCulture ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture);
	}

	public IEnumerable<TRecord> GetRecords()
		=> csvReader.GetRecords<TRecord>();

	public void Dispose() {
		csvReader?.Dispose();
		streamReader?.Dispose();
		stream?.Dispose();
	}
}
