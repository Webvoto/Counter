using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Counter {

	public class ElectionResultCollection {

		private readonly ConcurrentDictionary<string, ElectionResult> electionResults = new(StringComparer.InvariantCultureIgnoreCase);

		public ElectionResult GetOrAddElection(string electionId, Func<ElectionResult> creator = null)
			=> electionResults.GetOrAdd(electionId, k => creator != null ? creator.Invoke() : new ElectionResult(electionId));
			
		public IEnumerable<ElectionResult> ElectionResults => electionResults.Values;
	}

	public class ElectionResult {

		private readonly ConcurrentDictionary<string, PartyResult> partyResults;

		public string Id { get; }

		public string Name { get; }

		public string DisplayName => !string.IsNullOrWhiteSpace(Name) ? Name : Id;

		public ElectionResult(string id, string name = null) {
			Id = id;
			Name = name;
			var blankParty = PartyResult.CreateBlank();
			var nullParty = PartyResult.CreateNull();
			partyResults = new ConcurrentDictionary<string, PartyResult>(StringComparer.InvariantCultureIgnoreCase) {
				[blankParty.Identifier] = blankParty,
				[nullParty.Identifier] = nullParty,
			};
		}

		public PartyResult GetOrAddParty(string identifier, Func<PartyResult> creator = null)
			=> partyResults.GetOrAdd(identifier, k => creator != null ? creator.Invoke() : new PartyResult(identifier));

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
					return $"{Number}. {Name}";
				} else if (HasNumber) {
					return $"Chapa {Number}";
				} else if (HasName) {
					return $"Chapa {Name}";
				} else {
					return Identifier;
				}
			}
		}

		private int votes;

		public int Votes => votes;

		public PartyResult(string identifier, string name = null, int? number = null) {
			Identifier = identifier;
			Name = name;
			Number = number;
			votes = 0;
		}

		public void Increment() {
			Interlocked.Increment(ref votes);
		}

		public static PartyResult CreateBlank() => new(BlankIdentifier);
		
		public static PartyResult CreateNull() => new(NullIdentifier);
	}
}
