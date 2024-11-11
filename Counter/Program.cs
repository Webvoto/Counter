using Counter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

try {
	await runAsync(args);
	return 0;
} catch (Exception ex) {
	Console.WriteLine($"FATAL: {ex}");
	return 1;
}

async static Task runAsync(string[] args) {

	var sigCertPath = args.ElementAtOrDefault(0);
	var votesCsvPath = args.ElementAtOrDefault(1);
	var decKeyPath = args.ElementAtOrDefault(2);
	var partiesCsvPath = args.ElementAtOrDefault(3);

	if (string.IsNullOrEmpty(sigCertPath) || string.IsNullOrEmpty(votesCsvPath)) {
		Console.WriteLine("Syntax: Counter <signature certificate path> <votes CSV path> [<decryption key path>] [<parties CSV path>]");
		return;
	}

	var signatureCertificateFile = checkPath(sigCertPath);
	var votesCsvFile = checkPath(votesCsvPath);
	var decryptionKeyFile = !string.IsNullOrEmpty(decKeyPath) ? checkPath(decKeyPath) : null;
	var partiesCsvFile = !string.IsNullOrEmpty(partiesCsvPath) ? checkPath(partiesCsvPath) : null;

	// Parties are only needed later, but we'll read them ahead of time to raise exceptions sooner rather than later
	var parties = partiesCsvFile != null ? PartiesCsvReader.Read(partiesCsvFile) : null;

	var degreeOfParallelismVar = Environment.GetEnvironmentVariable("COUNTER_WORKERS");
	var degreeOfParallelism = !string.IsNullOrEmpty(degreeOfParallelismVar) ? int.Parse(degreeOfParallelismVar) : 32;
	Console.WriteLine($"Degree of parallelism: {degreeOfParallelism}");

	var counter = new VoteCounter();
	counter.Initialize(signatureCertificateFile, decryptionKeyFile);
	var results = await counter.CountAsync(votesCsvFile, degreeOfParallelism);

	if (results == null) {
		// decryption key not given, votes only checked
		Console.WriteLine("ALL VOTES ARE VALID");
		return;
	}

	var resultsWriter = new ResultsCsvWriter(parties);
	byte[] resultsFileBytes;
	using (var buffer = new MemoryStream()) {
		resultsWriter.Write(results, buffer);
		resultsFileBytes = buffer.ToArray();
	}

	var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HHmmss");

	var resultsFilePath = $"results-{timestamp}.csv";
	File.WriteAllBytes(resultsFilePath, resultsFileBytes);
	Console.WriteLine($"Results written to {resultsFilePath}");

	var signatureKeyParamsFile = new FileInfo(Path.ChangeExtension(signatureCertificateFile.FullName, ".json"));
	if (signatureKeyParamsFile.Exists) {
		Console.WriteLine("Signing results ...");
		var signatureKeyParams = WebVaultKeyParameters.Deserialize(File.ReadAllText(signatureKeyParamsFile.FullName));
		var webVaultClient = new WebVaultClient(signatureKeyParams.Endpoint, signatureKeyParams.ApiKey);
		var cms = await webVaultClient.SignCadesAsync(signatureKeyParams.KeyId, resultsFileBytes, File.ReadAllBytes(signatureCertificateFile.FullName));
		var cmsPath = $"signed-results-{timestamp}.p7s";
		File.WriteAllBytes($"signed-results-{timestamp}.p7s", cms);
		Console.WriteLine($"Signed results written to '{cmsPath}'");
	}
}

static FileInfo checkPath(string path) {
	var file = new FileInfo(path);
	if (!file.Exists) {
		throw new Exception($"File not found: '{file.FullName}'");
	}
	return file;
}
