using Counter.Database;
using Counter.Entities;
using Counter.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Counter.Services {
	public class VotePoolCounter {
		private readonly ServerInstanceCache serverInstanceCache;
		private readonly VoteCryptoService voteCryptoService;
		private readonly ILogger<VotePoolCounter> logger;
		private readonly AppDbContext appDbContext;

		public VotePoolCounter(ServerInstanceCache serverInstanceCache, VoteCryptoService voteCryptoService, ILogger<VotePoolCounter> logger, AppDbContext appDbContext) {
			this.serverInstanceCache = serverInstanceCache;
			this.voteCryptoService = voteCryptoService;
			this.logger = logger;
			this.appDbContext = appDbContext;
		}

		public async Task<Dictionary<string, Dictionary<string, int>>> CountAndVerifyVoteSlotsOnVotePoolAsync(VotePool votePool, bool verifyCertificateSignature) {
			var voteSlots = await appDbContext.VoteSlots.Where(vs => vs.PoolId == votePool.Id && vs.HasValue).ToListAsync();

			var result = new Dictionary<string, Dictionary<string, int>>();
			foreach (var voteSlot in voteSlots) {
				VerifySignatures(voteSlot, votePool, verifyCertificateSignature);
				CountVote(voteSlot, result);
			}

			return result;
		}

		public void VerifySignatures(VoteSlot voteSlot, VotePool votePool, bool verifyCertificateSignature) {
			var vote = voteSlot.GetVote();

			// Certificate signature (ICP-Brasil)
			if (verifyCertificateSignature && !voteCryptoService.VerifyVoteSignature(vote)) {
				logger.LogError($"Vote signature invalid for vote slot {voteSlot.Slot} on pool {votePool.Id}");
			}

			// Server signature (RSA)
			bool serverSignatureOk = false;
			if (votePool.VoteBoxId.HasValue) { // Votos presenciais podem ter sido registrados por qualquer servidor
				serverSignatureOk = serverInstanceCache.ServerInstances.Any(id => voteCryptoService.VerifyServerSignature(vote, id));
			} else { // Votos web foram registrados pelo servidor que criou o pool
				serverSignatureOk = voteCryptoService.VerifyServerSignature(vote, votePool.ServerInstanceId.Value);
			}

			if (!serverSignatureOk) {
				logger.LogError($"Server signature invalid for vote slot {voteSlot.Slot} on pool {votePool.Id}");
			}
		}

		public void CountVote(VoteSlot voteSlot, Dictionary<string, Dictionary<string, int>> results) {
			var vote = voteSlot.GetVote();

			foreach (var encryptedChoice in vote.EncryptedChoices) {
				var choice = voteCryptoService.DecryptVoteChoice(encryptedChoice);

				Dictionary<string, int> electionResults;
				if (!results.TryGetValue(encryptedChoice.ElectionId.ToString(), out electionResults)) {
					electionResults = new Dictionary<string, int>();
					results[encryptedChoice.ElectionId.ToString()] = electionResults;
				}

				electionResults[choice] = electionResults.TryGetValue(choice, out var value) ? value + 1 : 1;
			}
		}
	}
}
