using Counter.Classes;
using Counter.Database;
using Counter.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Counter.Services {
	public class DatabaseCounter {
		private readonly VotePoolRepository votePoolRepository;
		private readonly VotePoolCounter votePoolCounter;
		private readonly ILogger<DatabaseCounter> logger;
		private readonly AppDbContext appDbContext;
		private readonly VoteCryptoService voteCryptoService;

		public DatabaseCounter(VotePoolRepository votePoolRepository, VotePoolCounter votePoolCounter, ILogger<DatabaseCounter> logger, AppDbContext appDbContext, VoteCryptoService voteCryptoService) {
			this.votePoolRepository = votePoolRepository;
			this.votePoolCounter = votePoolCounter;
			this.logger = logger;
			this.appDbContext = appDbContext;
			this.voteCryptoService = voteCryptoService;
		}

		public async Task CountAndVerifyAllVotePoolsAsync(int votePoolBatchSize = 10) {
			var votePoolCount = await votePoolRepository.CountVotePoolsAsync();
			var offset = 0;

			if (votePoolCount == 0) {
				logger.LogInformation("No vote pools found to count");
				return;
			}

			logger.LogInformation($"Found {votePoolCount} vote pools with votes to read");

			bool verifyCertificateSignature = true;
			if (voteCryptoService.VoteSigningCertificate.Thumbprint != Constants.VoteSigningCertificateThumbprint) {
				logger.LogWarning("The vote signing certificate found in the database is not the expected certificate. Thus, certificate signature checking will not be made (server signatures will still be checked).");
				verifyCertificateSignature = false;
			}

			var aggregateResults = new Dictionary<string, Dictionary<string, int>>();
			int votePoolIndex = 0;
			do {
				var votePools = await votePoolRepository.GetVotePoolsPaginatedAsync(offset, votePoolBatchSize);

				foreach (var votePool in votePools) {
					var poolResults = await votePoolCounter.CountAndVerifyVoteSlotsOnVotePoolAsync(votePool, verifyCertificateSignature);
					aggregatePoolResults(aggregateResults, poolResults);
					votePoolIndex++;
					logger.LogInformation($"Read vote pool with Id {votePool.Id} | ({votePoolIndex} of {votePoolCount})");
				}

				offset += votePoolBatchSize;
			} while (offset < votePoolCount);

			await formatAndOutputResultsAsync(aggregateResults);
		}

		private async Task formatAndOutputResultsAsync(Dictionary<string, Dictionary<string, int>> results) {
			var elections = await appDbContext.Elections.Include(e => e.Office).ToListAsync();
			var parties = await appDbContext.Parties.ToListAsync();

			foreach (var election in elections) {
				if (!results.TryGetValue(election.Id.ToString(), out var electionResults)) {
					electionResults = new Dictionary<string, int>();
				}

				foreach (var party in parties.Where(p => p.ElectionId == election.Id)) {
					if (!electionResults.ContainsKey(party.Id.ToString())) {
						electionResults[party.Id.ToString()] = 0;
					}
				}

				if (!electionResults.ContainsKey(Constants.VoteTypes.Null)) {
					electionResults[Constants.VoteTypes.Null] = 0;
				}

				if (!electionResults.ContainsKey(Constants.VoteTypes.Blank)) {
					electionResults[Constants.VoteTypes.Blank] = 0;
				}

				results[election.Id.ToString()] = electionResults;
			}

			Console.WriteLine("\nRESULTADOS:");
			var resultElections = results.Keys.Select(k => elections.First(e => e.Id == Guid.Parse(k)));
			foreach (var election in elections.OrderBy(e => e.Office.Order)) {
				Console.WriteLine($"\n{election.Office.Name}");
				var electionResults = results[election.Id.ToString()];

				var resultParties = electionResults.Keys.Where(k => Guid.TryParse(k, out _)).Select(k => parties.First(p => p.Id == Guid.Parse(k)));

				foreach(var party in resultParties.OrderBy(p => p.PriorityScore)) {
					Console.WriteLine($"=> {party.Name.Trim()}: {electionResults[party.Id.ToString()]}");
				}

				Console.WriteLine($"=> Nulo: {electionResults[Constants.VoteTypes.Null]}");
				Console.WriteLine($"=> Branco: {electionResults[Constants.VoteTypes.Blank]}");
			}
		}

		private void aggregatePoolResults(Dictionary<string, Dictionary<string, int>> aggregateResults, Dictionary<string, Dictionary<string, int>> poolResults) {
			foreach (var electionId in poolResults.Keys) {
				var electionResults = new Dictionary<string, int>();

				if (!aggregateResults.TryGetValue(electionId, out electionResults)) {
					electionResults = new Dictionary<string, int>();
					aggregateResults[electionId] = electionResults;
				}

				foreach (var choice in poolResults[electionId].Keys) {
					int choiceResults;
					if (!electionResults.TryGetValue(choice, out choiceResults)) {
						choiceResults = 0;
					}

					choiceResults += poolResults[electionId][choice];

					aggregateResults[electionId][choice] = choiceResults;
				}
			}
		}
	}
}
