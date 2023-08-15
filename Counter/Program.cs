using Counter;
using System;
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
	var decKeyParamsPath = args.ElementAtOrDefault(2);
	var partiesCsvPath = args.ElementAtOrDefault(3);

	if (string.IsNullOrEmpty(sigCertPath) || string.IsNullOrEmpty(votesCsvPath)) {
		Console.WriteLine("Syntax: Counter <signature certificate path> <votes CSV path> [<decryption key parameters path>] [<parties CSV path>]");
		return;
	}

	var signatureCertificateFile = checkPath(sigCertPath);
	var votesCsvFile = checkPath(votesCsvPath);
	var decryptionKeyParamsFile = !string.IsNullOrEmpty(decKeyParamsPath) ? checkPath(decKeyParamsPath) : null;
	var partiesCsvFile = !string.IsNullOrEmpty(partiesCsvPath) ? checkPath(partiesCsvPath) : null;

	var degreeOfParallelismVar = Environment.GetEnvironmentVariable("COUNTER_WORKERS");
	var degreeOfParallelism = !string.IsNullOrEmpty(degreeOfParallelismVar) ? int.Parse(degreeOfParallelismVar) : 32;
	Console.WriteLine($"Degree of parallelism: {degreeOfParallelism}");

	var counter = new VoteCounter();
	counter.Initialize(signatureCertificateFile, decryptionKeyParamsFile);
	var results = await counter.CountAsync(votesCsvFile, partiesCsvFile, degreeOfParallelism);

	if (results == null) {
		// decryption key not given, votes only checked
		Console.WriteLine("ALL VOTES ARE VALID");
		return;
	}

	var formattedResults = formatResults(results);
	Console.Write(formattedResults);

	var signatureKeyParamsFile = new FileInfo(Path.ChangeExtension(signatureCertificateFile.FullName, ".json"));
	if (signatureKeyParamsFile.Exists) {
		Console.WriteLine("Signing results ...");
		var signatureKeyParams = WebVaultKeyParameters.Deserialize(File.ReadAllText(signatureKeyParamsFile.FullName));
		var webVaultClient = new WebVaultClient(signatureKeyParams.Endpoint, signatureKeyParams.ApiKey);
		var cms = await webVaultClient.SignCadesAsync(signatureKeyParams.KeyId, Encoding.UTF8.GetBytes(formattedResults), File.ReadAllBytes(signatureCertificateFile.FullName));
		var cmsPath = $"signed-results-{DateTime.Now:yyyy-MM-dd-HHmmss}.p7s";
		File.WriteAllBytes($"signed-results-{DateTime.Now:yyyy-MM-dd-HHmmss}.p7s", cms);
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

static string formatResults(ElectionResultCollection results) {

	var text = new StringBuilder();

	var partyHeader = "Chapa";
	var votesHeader = "Votos";

	foreach (var election in results.ElectionResults.OrderBy(e => e.DisplayName)) {

		var partyColumnLen = Math.Max(partyHeader.Length, election.PartyResults.Max(p => p.DisplayName.Length));
		var votesColumnLen = Math.Max(votesHeader.Length, election.PartyResults.Max(p => p.Votes.ToString("N0").Length));

		text.AppendLine();
		text.AppendLine();
		text.AppendLine($"{new string('=', election.DisplayName.Length + 2)}");
		text.AppendLine($" {election.DisplayName}");
		text.AppendLine($"{new string('=', election.DisplayName.Length + 2)}");
		text.AppendLine();
		text.AppendLine($"+-{new string('-', partyColumnLen)}-+-{new string('-', votesColumnLen)}-+");
		text.AppendLine($"| {partyHeader.PadRight(partyColumnLen)} | {votesHeader.PadLeft(votesColumnLen)} |");
		text.AppendLine($"+-{new string('-', partyColumnLen)}-+-{new string('-', votesColumnLen)}-+");

		foreach (var party in election.PartyResults.OrderBy(p => p.IsBlankOrNull ? 1 : 0).ThenByDescending(p => p.Votes)) {
			text.AppendLine($"| {party.DisplayName.PadRight(partyColumnLen)} | {party.Votes.ToString("N0").PadLeft(votesColumnLen)} |");
		}

		text.AppendLine($"+-{new string('-', partyColumnLen)}-+-{new string('-', votesColumnLen)}-+");
		text.AppendLine();
	}

	return text.ToString();
}
