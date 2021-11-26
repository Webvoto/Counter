using Counter.Database;
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
	public class ServerInstanceCache {
		private readonly AppDbContext appDbContext;
		private readonly ILogger<ServerInstanceCache> logger;

		public ServerInstanceCache(AppDbContext appDbContext, ILogger<ServerInstanceCache> logger) {
			this.appDbContext = appDbContext;
			this.logger = logger;
		}

		public Dictionary<int, byte[]> PublicKeys { get; private set; }

		public Dictionary<int, RsaService> Rsa { get; private set; }

		public List<int> ServerInstances
			=> PublicKeys.Keys.ToList();

		public async Task InitializeAsync() {
			var serverInstances = await appDbContext.ServerInstances.Where(s => s.PublicKey != null).ToListAsync();
			PublicKeys = serverInstances.ToDictionary(s => s.Id, s => s.PublicKey);
			Rsa = PublicKeys.ToDictionary(pk => pk.Key, pk => new RsaService(pk.Value));
			logger.LogInformation($"Loaded server keys");
		}
	}
}
