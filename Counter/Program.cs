using Counter;
using System;
using System.IO;
using System.Linq;
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
	var decKeyParamsPath = args.ElementAtOrDefault(1);
	var votesCsvPath = args.ElementAtOrDefault(2);
	var partiesCsvPath = args.ElementAtOrDefault(3);

	if (string.IsNullOrEmpty(sigCertPath) || string.IsNullOrEmpty(decKeyParamsPath) || string.IsNullOrEmpty(votesCsvPath)) {
		Console.WriteLine("Syntax: Counter <signature certificate path> <decryption key parameters path> <votes CSV path> [<parties CSV path>]");
		return;
	}

	var signatureCertificateFile = checkPath(sigCertPath);
	var decryptionKeyParamsFile = checkPath(decKeyParamsPath);
	var votesCsvFile = checkPath(votesCsvPath);
	var partiesCsvFile = !string.IsNullOrEmpty(partiesCsvPath) ? checkPath(partiesCsvPath) : null;

	var degreeOfParallelismVar = Environment.GetEnvironmentVariable("COUNTER_WORKERS");
	var degreeOfParallelism = !string.IsNullOrEmpty(degreeOfParallelismVar) ? int.Parse(degreeOfParallelismVar) : 32;
	Console.WriteLine($"Degree of parallelism: {degreeOfParallelism}");

	var counter = new VoteCounter();
	counter.Initialize(signatureCertificateFile, decryptionKeyParamsFile);
	var results = await counter.CountAsync(votesCsvFile, partiesCsvFile, degreeOfParallelism);

	printResults(results);
}

static FileInfo checkPath(string path) {
	var file = new FileInfo(path);
	if (!file.Exists) {
		throw new Exception($"File not found: '{file.FullName}'");
	}
	return file;
}

static void printResults(ElectionResultCollection results) {
	
	foreach (var election in results.ElectionResults) {

		var partyColumnLen = Math.Max("Chapa".Length, election.PartyResults.Max(p => p.DisplayName.Length));
		var votesColumnLen = Math.Max("Votos".Length, election.PartyResults.Max(p => $"{p.Votes:N0}".Length));

		Console.WriteLine();
		Console.WriteLine();
		Console.WriteLine($"{new string('=', election.DisplayName.Length)}");
		Console.WriteLine($"{election.DisplayName}");
		Console.WriteLine($"{new string('=', election.DisplayName.Length)}");
		Console.WriteLine();
		Console.WriteLine($"+-{new string('-', partyColumnLen)}-+-{new string('-', votesColumnLen)}-+");
		Console.WriteLine($"| {"Chapa".PadRight(partyColumnLen)} | {"Votos".PadLeft(votesColumnLen)} |");
		Console.WriteLine($"+-{new string('-', partyColumnLen)}-+-{new string('-', votesColumnLen)}-+");

		foreach (var party in election.PartyResults.OrderBy(p => p.IsBlankOrNull ? 1 : 0).ThenByDescending(p => p.Votes)) {
			Console.WriteLine($"| {party.DisplayName.PadRight(partyColumnLen)} | {party.Votes.ToString("N0").PadLeft(votesColumnLen)} |");
		}

		Console.WriteLine($"+-{new string('-', partyColumnLen)}-+-{new string('-', votesColumnLen)}-+");
		Console.WriteLine();
	}
}
