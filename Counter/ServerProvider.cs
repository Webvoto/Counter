using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Counter {
	public class ServerProvider {

		public class Server {

			public int Id { get; set; }

			public int VotingEventSignatureVersion { get; set; }

			public ECDsa PublicKey { get; set; }
		}

		private readonly Dictionary<int, Server> servers = new();

		public void Initialize(FileInfo serversCsvFile) {
			Console.Write("Reading server keys ...");
			using (var votesCsvReader = ServersCsvReader.Open(serversCsvFile)) {
				foreach (var voteRecord in votesCsvReader.GetRecords()) {
					if (!servers.ContainsKey(voteRecord.Id)) {
						servers[voteRecord.Id] = new Server() {
							Id = voteRecord.Id,
							VotingEventSignatureVersion = voteRecord.VotingEventSignatureVersion,
							PublicKey = Util.GetPublicKey(Util.DecodeHex(voteRecord.PublicKey))
						};
						Console.Write(".");
					}
				}
			}
		}

		public Server GetRequiredServer(int serverId) {

			if (!servers.TryGetValue(serverId, out Server server)) {
				throw new Exception($"Server not provided {serverId}");
			}

			return server;
		}
	}
}
