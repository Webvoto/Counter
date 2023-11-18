using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Counter {

	public class ElectionResultCollection {

		private readonly ConcurrentDictionary<string, ElectionResult> electionResults = new(StringComparer.InvariantCultureIgnoreCase);

		public ElectionResult GetOrAddElection(string electionId)
			=> electionResults.GetOrAdd(electionId, new ElectionResult(electionId));
			
		public IEnumerable<ElectionResult> ElectionResults => electionResults.Values;
	}

	public class ElectionResult {

		private readonly ConcurrentDictionary<string, DistrictResult> districtResults;

		public string Id { get; }

		public ElectionResult(string id) {
			Id = id;
			districtResults = new ConcurrentDictionary<string, DistrictResult>(StringComparer.InvariantCultureIgnoreCase);
		}
		
		public DistrictResult GetOrAddDistrict(string id)
			=> districtResults.GetOrAdd(id ?? "", new DistrictResult(id));

		public IEnumerable<DistrictResult> DistrictResults => districtResults.Values;
	}

	public class DistrictResult {

		private readonly ConcurrentDictionary<string, PartyResult> partyResults;

		public string Id { get; }

		public DistrictResult(string id) {
			Id = id;
			partyResults = new ConcurrentDictionary<string, PartyResult>(StringComparer.InvariantCultureIgnoreCase);
		}

		public PartyResult GetOrAddParty(string identifier)
			=> partyResults.GetOrAdd(identifier, new PartyResult(identifier));

		public IEnumerable<PartyResult> PartyResults => partyResults.Values;
	}

	public class PartyResult {

		public const string BlankIdentifier = "Blank";
		public const string NullIdentifier = "Null";

		public string Identifier { get; }

		public bool IsBlank => Identifier == BlankIdentifier;

		public bool IsNull => Identifier == NullIdentifier;

		public bool IsBlankOrNull => IsBlank || IsNull;

		private int votes;

		public int Votes => votes;

		public PartyResult(string identifier) {
			Identifier = identifier;
			votes = 0;
		}

		public void Increment() {
			Interlocked.Increment(ref votes);
		}
	}
}
