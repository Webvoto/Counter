using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;

namespace Counter {

	public class ElectionResultCollection {

		private readonly Dictionary<string, ElectionResult> electionResults = new(StringComparer.InvariantCultureIgnoreCase);

		public ElectionResult GetOrAddElection(string electionId, Func<ElectionResult> creator = null) {
			if (!electionResults.TryGetValue(electionId, out var electionResult)) {
				electionResults[electionId] = electionResult = creator?.Invoke() ?? new ElectionResult(electionId);
			}
			return electionResult;
		}

		public IEnumerable<ElectionResult> ElectionResults => electionResults.Values;
	}

	public class ElectionResult {

		private readonly Dictionary<string, PartyResult> partyResults = new(StringComparer.InvariantCultureIgnoreCase);

		public string Id { get; }

		public string Name { get; }

		public string DisplayName => !string.IsNullOrWhiteSpace(Name) ? Name : Id;

		public ElectionResult(string id, string name = null) {
			Id = id;
			Name = name;
			addParty(PartyResult.CreateBlank());
			addParty(PartyResult.CreateNull());
		}

		public PartyResult GetOrAddParty(string identifier, Func<PartyResult> creator = null) {
			if (!partyResults.TryGetValue(identifier, out var partyResult)) {
				partyResult = addParty(creator?.Invoke() ?? new PartyResult(identifier));
			}
			return partyResult;
		}

		private PartyResult addParty(PartyResult partyResult) {
			partyResults[partyResult.Identifier] = partyResult;
			return partyResult;
		}

		public IEnumerable<PartyResult> PartyResults => partyResults.Values;
	}

	public class PartyResult {

		private const string BlankIdentifier = "Blank";
		private const string NullIdentifier = "Null";

		public string Identifier { get; }

		public bool IsBlankOrNull => Identifier == BlankIdentifier || Identifier == NullIdentifier;

		public string Name { get; }

		public bool HasName => !string.IsNullOrEmpty(Name);

		public int? Number { get; }

		public bool HasNumber => Number.HasValue;

		public string DisplayName {
			get {
				if (IsBlankOrNull) {
					return Identifier == BlankIdentifier ? "Votos brancos" : "Votos nulos";
				} else if (HasName && HasNumber) {
					return $"Chapa {Number}: {Name}";
				} else if (HasNumber) {
					return $"Chapa {Number}";
				} else if (HasName) {
					return $"Chapa {Name}";
				} else {
					return Identifier;
				}
			}
		}

		public int Votes { get; private set; }

		public PartyResult(string identifier, string name = null, int? number = null) {
			Identifier = identifier;
			Name = name;
			Number = number;
			Votes = 0;
		}

		public void Increment() {
			++Votes;
		}

		public static PartyResult CreateBlank() => new(BlankIdentifier);
		
		public static PartyResult CreateNull() => new(NullIdentifier);
	}
}
