using Counter.Csv;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Counter;

public class Server {

	public int Id { get; set; }

	public int VotingEventSignatureVersion { get; set; }

	public int OptionSignatureVersion { get; set; }

	public ECDsa PublicKey { get; set; }
}

public class ServerProvider {

	private readonly Dictionary<int, Server> servers = new();

	public void Initialize(FileInfo serversCsvFile) {
		
		Console.WriteLine("Reading servers ...");
		
		using var serversCsvReader = ServersCsvReader.Open(serversCsvFile);
		
		foreach (var serverRecord in serversCsvReader.GetRecords()) {
			if (!servers.ContainsKey(serverRecord.Id)) {
				servers[serverRecord.Id] = new Server() {
					Id = serverRecord.Id,
					VotingEventSignatureVersion = serverRecord.VotingEventSignatureVersion,
					OptionSignatureVersion = serverRecord.OptionSignatureVersion,
					PublicKey = Util.GetPublicKey(Util.DecodeHex(serverRecord.PublicKey)),
				};
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
