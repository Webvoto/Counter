using Counter;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

const string SigCertBaseName = "signature-certificate";
const string ServersCsvFile = "servers.csv";
const string VotesCsvFile = "votes.csv";
const string DecryptionKeyFile = "decryption-key.pem";
const string PartiesCsvFile = "parties.csv";
const string VotingEventsCsvFile = "events.csv";
const string DistrictsCsvFile = "districts.csv";

try {
	await runAsync(args);
	return 0;
} catch (Exception ex) {
	Console.WriteLine($"FATAL: {ex}");
	return 1;
}

async static Task runAsync(string[] args) {

	var basePath = args.ElementAtOrDefault(0) ?? Directory.GetCurrentDirectory();
	var baseDir = new DirectoryInfo(basePath);
	if (!baseDir.Exists) {
		throw new Exception($"Base directory not found: {baseDir.FullName}");
	}

	var signatureCertificateFile = getFileInfo(baseDir, $"{SigCertBaseName}.pem");
	if (!signatureCertificateFile.Exists) {
		signatureCertificateFile = getFileInfo(baseDir, $"{SigCertBaseName}.cer");
	}
	var serversCsvFile = getFileInfo(baseDir, ServersCsvFile);
	var votesCsvFile = getFileInfo(baseDir, VotesCsvFile);
	var decryptionKeyFile = getFileInfo(baseDir, DecryptionKeyFile);
	var partiesCsvFile = getFileInfo(baseDir, PartiesCsvFile);
	var votingEventsCsvFile = getFileInfo(baseDir, VotingEventsCsvFile);
	var districtsCsvFile = getFileInfo(baseDir, DistrictsCsvFile);

	ensureFileExists(serversCsvFile);
	var serverProvider = new ServerProvider();
	serverProvider.Initialize(serversCsvFile);

	var degreeOfParallelismVar = Environment.GetEnvironmentVariable("COUNTER_WORKERS");
	var degreeOfParallelism = !string.IsNullOrEmpty(degreeOfParallelismVar) ? int.Parse(degreeOfParallelismVar) : 32;
	Console.WriteLine($"Degree of parallelism: {degreeOfParallelism}");
	Console.WriteLine();

	if (votesCsvFile.Exists) {

		ensureFileExists(signatureCertificateFile);

		// Parties and districts are only needed later, but we'll read them ahead of time to raise exceptions sooner rather than later
		var parties = partiesCsvFile.Exists ? PartiesCsvReader.Read(partiesCsvFile) : null;
		var districts = districtsCsvFile.Exists ? DistrictsCsvReader.Read(districtsCsvFile) : null;

		var counter = new VoteCounter(serverProvider);
		counter.Initialize(signatureCertificateFile, decryptionKeyFile);
		var results = await counter.CountAsync(votesCsvFile, degreeOfParallelism);

		if (results == null) {
			// decryption key not given, votes only checked
			Console.WriteLine("ALL VOTES ARE VALID");
			return;
		}

		var resultsWriter = new ResultsCsvWriter(parties, districts);
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

	if (votingEventsCsvFile.Exists) {

		Console.WriteLine();
		Console.WriteLine($"Initializing voting events check...");
		var eventValidator = new VotingEventValidator(serverProvider);
		await eventValidator.ValidateAsync(votingEventsCsvFile, degreeOfParallelism);

	}
}

static FileInfo getFileInfo(DirectoryInfo baseDir, string fileName) => new FileInfo(Path.Combine(baseDir.FullName, fileName));

static void ensureFileExists(FileInfo file) {
	if (!file.Exists) {
		throw new Exception($"File not found: {file.FullName}");
	}
}
