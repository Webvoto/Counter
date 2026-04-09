using Counter;
using Counter.Csv;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Webvoto.VotingSystem.Auditing;

const string ServersFileName = "6-servers.csv";
const string VotesFileName = "10-votos.csv";
const string SignatureCertFileName = "signature-certificate.cer";
const string DecryptionKeyFilePattern = "chave-*-privada.pem";
const string OptionsFileName = "5-chapas.csv";
const string DistrictsFileName = "11-distritos.csv";
const string VotingEventsFileName = "7-voting-events.csv";

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
	var decryptionKeyFiles = baseDir.GetFiles(DecryptionKeyFilePattern);
	var optionsCsvFile = getFileInfo(baseDir, OptionsFileName);
	var districtsCsvFile = getFileInfo(baseDir, DistrictsFileName);
	var votingEventsCsvFile = getFileInfo(baseDir, VotingEventsFileName);

	ensureFileExists(serversCsvFile);
	var serverProvider = new ServerProvider();
	serverProvider.Initialize(serversCsvFile);

	if (votesCsvFile.Exists) {

		ensureFileExists(signatureCertificateFile);

		var options = optionsCsvFile.Exists ? OptionsCsvReader.Read(optionsCsvFile) : null;
		var districts = districtsCsvFile.Exists ? DistrictsCsvReader.Read(districtsCsvFile) : null;

		if (options != null) {
			foreach (var option in options) {
				if (!option.ServerInstanceId.HasValue) {
					Console.WriteLine($"[WARNING] Option {option.Id} is not signed");
				} else {
					var server = serverProvider.GetRequiredServer(option.ServerInstanceId.Value);
					if (!server.PublicKey.VerifyData(OptionEncoding.Encode(option, server.OptionSignatureVersion), option.ServerSignature, HashAlgorithmName.SHA256)) {
						Console.WriteLine($"[WARNING] Option {option.Id} has an invalid signature");
					}
				}
			}
		}

		var counter = new VoteCounter(serverProvider);
		counter.Initialize(signatureCertificateFile, decryptionKeyFiles);
		var results = await counter.CountAsync(votesCsvFile, degreeOfParallelism);

		if (results == null) {

			// decryption key not given, votes only checked
			Console.WriteLine("ALL VOTES ARE VALID");

		} else {

			var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HHmmss");

			var resultsFileName = $"results-{timestamp}.csv";
			var resultsFile = getFileInfo(baseDir, resultsFileName);
			using (var resultsFileStream = resultsFile.Create()) {
				var resultsWriter = new ResultsCsvWriter(options, districts);
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
