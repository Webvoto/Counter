using Counter.Classes;
using Counter.Database;
using Counter.Repositories;
using Counter.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Counter {
	class Program {
		public async static Task Main(string[] args) {
			if (args.Length < 2 || args.Length > 3) {
				Console.WriteLine("Please, provided the connection string, the path to the private key (PEM format) and the password to the key as arguments");
				return;
			}

			var connectionString = args[0];
			var encryptionKeyPath = args[1];
			var encryptionKeyPassword = args.ElementAtOrDefault(2);

			var host = Host.CreateDefaultBuilder()
				.ConfigureServices(s => ConfigureServices(s, connectionString))
				.ConfigureLogging(ConfigureLogging).Build();

			await InitializeServicesAsync(host, encryptionKeyPath, encryptionKeyPassword);

			var databaseCounter = host.Services.GetRequiredService<DatabaseCounter>();
			await databaseCounter.CountAndVerifyAllVotePoolsAsync();
		}

		public async static Task InitializeServicesAsync(IHost host, string encryptionKeyPath, string encryptionKeyPassword) {
			var serverInstancePublicKeyCache = host.Services.GetRequiredService<ServerInstanceCache>();
			await serverInstancePublicKeyCache.InitializeAsync();

			var voteCryptoService = host.Services.GetRequiredService<VoteCryptoService>();
			var privateKeyContent = File.ReadAllText(encryptionKeyPath);
			var decodedKey = Util.DecodePem(privateKeyContent);
			await voteCryptoService.InitializeAsync(encryptionKeyPassword, decodedKey);
		}

		public static void ConfigureServices(IServiceCollection services, string connectionString) {
			services.AddDbContext<AppDbContext>(options => {
				options.UseSqlServer(connectionString);
			});

			services.AddSingleton<ServerInstanceCache>();
			services.AddSingleton<VoteCryptoService>();

			services.AddSingleton<DatabaseCounter>();
			services.AddSingleton<VotePoolCounter>();

			services.AddTransient<VotePoolRepository>();
		}

		public static void ConfigureLogging(HostBuilderContext context, ILoggingBuilder logging) {
			logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
		}
	}
}
