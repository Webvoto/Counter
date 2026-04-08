using Counter;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

const string ServersFileName = "servers.csv";
const string VotesFileName = "votes.csv";
const string SignatureCertFileName = "signature-certificate.cer";
const string DecryptionKeyFileName = "decryption-key.pem";
const string PartiesFileName = "parties.csv";
const string DistrictsFileName = "districts.csv";
const string VotingEventsFileName = "events.csv";

try {
	await runAsync(args);
	return 0;
} catch (Exception ex) {
	Console.WriteLine($"FATAL: {ex}");
	return 1;
}

async static Task runAsync(string[] args) {

	var degreeOfParallelismVar = Environment.GetEnvironmentVariable("COUNTER_THREADS");
	var degreeOfParallelism = !string.IsNullOrEmpty(degreeOfParallelismVar) ? int.Parse(degreeOfParallelismVar) : Environment.ProcessorCount;
	Console.WriteLine($"Degree of parallelism: {degreeOfParallelism}");

	var basePath = args.ElementAtOrDefault(0) ?? Directory.GetCurrentDirectory();
	var baseDir = new DirectoryInfo(basePath);
	if (!baseDir.Exists) {
		throw new Exception($"Base directory not found: {baseDir.FullName}");
	}

	var serversCsvFile = getFileInfo(baseDir, ServersFileName);
	var votesCsvFile = getFileInfo(baseDir, VotesFileName);
	var signatureCertificateFile = getFileInfo(baseDir, SignatureCertFileName);
	var decryptionKeyFile = getFileInfo(baseDir, DecryptionKeyFileName);
	var partiesCsvFile = getFileInfo(baseDir, PartiesFileName);
	var districtsCsvFile = getFileInfo(baseDir, DistrictsFileName);
	var votingEventsCsvFile = getFileInfo(baseDir, VotingEventsFileName);

	ensureFileExists(serversCsvFile);
	var serverProvider = new ServerProvider();
	serverProvider.Initialize(serversCsvFile);

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

		} else {

			var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HHmmss");

			var resultsFileName = $"results-{timestamp}.csv";
			var resultsFile = getFileInfo(baseDir, resultsFileName);
			using (var resultsFileStream = resultsFile.Create()) {
				var resultsWriter = new ResultsCsvWriter(parties, districts);
				resultsWriter.Write(results, resultsFileStream);
			}
			Console.WriteLine($"Results written to '{resultsFileName}'");

			var signatureKeyParamsFile = new FileInfo(Path.ChangeExtension(signatureCertificateFile.FullName, ".json"));
			if (signatureKeyParamsFile.Exists) {
				Console.WriteLine("Signing results ...");
				var signatureKeyParams = WebVaultKeyParameters.Deserialize(File.ReadAllText(signatureKeyParamsFile.FullName));
				var webVaultClient = new WebVaultClient(signatureKeyParams.Endpoint, signatureKeyParams.ApiKey);
				var cms = await webVaultClient.SignCadesAsync(signatureKeyParams.KeyId, File.ReadAllBytes(resultsFile.FullName), File.ReadAllBytes(signatureCertificateFile.FullName));
				var cmsPath = $"signed-results-{timestamp}.p7s";
				File.WriteAllBytes($"signed-results-{timestamp}.p7s", cms);
				Console.WriteLine($"Signed results written to '{cmsPath}'");
			}
		}
	}

	if (votingEventsCsvFile.Exists) {
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
