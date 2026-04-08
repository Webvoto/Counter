using Counter;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

const string SigCertBaseName = "sigCert";
const string ServersCsvFile = "servers.csv";
const string VotesCsvFile = "votes.csv";
const string DecKeyFile = "decKey.key";
const string PartiesCsvFile = "parties.csv";
const string VotingEventsCsvFile = "votingEvents.csv";
const string DistrictsCsvFile = "districts.csv";

try {
	await runAsync(args);
	return 0;
} catch (Exception ex) {
	Console.WriteLine($"FATAL: {ex}");
	return 1;
}

async static Task runAsync(string[] args) {

	var baseDir = args.ElementAtOrDefault(0) ?? Directory.GetCurrentDirectory();

	string sigCertPath = Path.Combine(baseDir, $"{SigCertBaseName}.pem");

	if (!File.Exists(sigCertPath)) {
		sigCertPath = Path.Combine(baseDir, $"{SigCertBaseName}.cer");
	}
	var serversCsvPath = Path.Combine(baseDir, ServersCsvFile);
	var votesCsvPath = Path.Combine(baseDir, VotesCsvFile);
	var decKeyPath = Path.Combine(baseDir, DecKeyFile);
	var partiesCsvPath = Path.Combine(baseDir, PartiesCsvFile);
	var votingEventsCsvPath = Path.Combine(baseDir, VotingEventsCsvFile);
	var districtsCsvPath = Path.Combine(baseDir, DistrictsCsvFile);
	
	if (!File.Exists(sigCertPath) || !File.Exists(votesCsvPath)) {
		Console.WriteLine("Erro: Arquivos necessários não encontrados.");
		Console.WriteLine($"Diretório atual de busca: {baseDir}");
		Console.WriteLine("Certifique-se de que os arquivos abaixo estão na pasta:");
		Console.WriteLine($"  - {SigCertBaseName}.cer OU {SigCertBaseName}.pem");
		Console.WriteLine($"  - {VotesCsvFile}");
		return;
	}

	var signatureCertificateFile = checkPath(sigCertPath);
	var serversCsvFile = checkPath(serversCsvPath);
	var votesCsvFile = checkPath(votesCsvPath);
	var decryptionKeyFile = !string.IsNullOrEmpty(decKeyPath) ? checkPath(decKeyPath) : null;
	var partiesCsvFile = !string.IsNullOrEmpty(partiesCsvPath) ? checkPath(partiesCsvPath) : null;
	var districtsCsvFile = !string.IsNullOrEmpty(districtsCsvPath) ? checkPath(districtsCsvPath) : null;
	var votingEventsCsvFile = !string.IsNullOrEmpty(votingEventsCsvPath) ? checkPath(votingEventsCsvPath) : null;

	// Parties and districts are only needed later, but we'll read them ahead of time to raise exceptions sooner rather than later
	var parties = partiesCsvFile != null ? PartiesCsvReader.Read(partiesCsvFile) : null;
	var districts = districtsCsvFile != null ? DistrictsCsvReader.Read(districtsCsvFile) : null;

	var degreeOfParallelismVar = Environment.GetEnvironmentVariable("COUNTER_WORKERS");
	var degreeOfParallelism = !string.IsNullOrEmpty(degreeOfParallelismVar) ? int.Parse(degreeOfParallelismVar) : 32;
	Console.WriteLine($"Degree of parallelism: {degreeOfParallelism}");

	var serverProvider = new ServerProvider();
	serverProvider.Initialize(serversCsvFile);
	
	Console.WriteLine();
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

	if (!File.Exists(votingEventsCsvPath)) {
		return;
	}

	Console.WriteLine();
	Console.WriteLine($"Initializing voting events check...");
	var eventValidator = new VotingEventValidator(serverProvider);
	await eventValidator.ValidateAsync(votingEventsCsvFile, degreeOfParallelism);

}

static FileInfo checkPath(string path) {
	var file = new FileInfo(path);
	if (!file.Exists) {
		throw new Exception($"File not found: '{file.FullName}'");
	}
	return file;
}
