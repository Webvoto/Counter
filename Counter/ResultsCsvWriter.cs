using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace Counter {

	public class ResultCsvRecord {

		public string ElectionId { get; set; }

		public string ElectionLabel { get; set; }

		public string PartyIdentifier { get; set; }

		public string PartyLabel { get; set; }

		public int Votes { get; set; }
	}

	public class ResultsCsvWriter {

		private const string BlankVotesLabel = "Votos brancos";
		private const string NullVotesLabel = "Votos nulos";

		private readonly List<PartyCsvRecord> parties;

		public ResultsCsvWriter(List<PartyCsvRecord> parties) {
			this.parties = parties;
		}

		public void Write(ElectionResultCollection results, Stream outStream) {

			var records = new List<ResultCsvRecord>();

			foreach (var electionResult in results.ElectionResults) {
				var electionLabel = getElectionLabel(electionResult);
				records.AddRange(getPartyRecords(electionResult.Id, electionLabel, electionResult.PartyResults));
			}

			var orderedRecords = records
				.OrderBy(r => r.ElectionLabel)
				.ThenBy(r => r.PartyLabel == BlankVotesLabel || r.PartyLabel == NullVotesLabel ? 1 : 0)
				.ThenByDescending(r => r.Votes);

			using var streamWriter = new StreamWriter(outStream, Encoding.UTF8);
			using var csvWriter = new CsvWriter(streamWriter, Util.CsvConfiguration);
			csvWriter.WriteRecords(orderedRecords);
		}

		private IEnumerable<ResultCsvRecord> getPartyRecords(string electionId, string electionLabel, IEnumerable<PartyResult> partyResults) {

			// Check which parties need to be nullified

			var nullifiedPartyResults = new List<PartyResult>();

			foreach (var partyResult in partyResults) {
				if (!partyResult.IsBlankOrNull) {
					var party = parties?.FirstOrDefault(p => p.PartyId.Equals(partyResult.Identifier, StringComparison.OrdinalIgnoreCase));
					if (party != null && !party.IsEnabled) {
						nullifiedPartyResults.Add(partyResult);
					}
				}
			}

			// Yield parties/blanks/nulls (except nullified ones)

			foreach (var partyResult in partyResults.Except(nullifiedPartyResults)) {
				yield return new ResultCsvRecord {
					ElectionId = electionId,
					ElectionLabel = electionLabel,
					PartyIdentifier = partyResult.Identifier,
					PartyLabel = getPartyLabel(partyResult),
					Votes = partyResult.Votes + (partyResult.IsNull ? nullifiedPartyResults.Sum(npr => npr.Votes) : 0),
				};
			}

			// Yield enabled parties without votes (not in `partyResults`)

			if (parties != null) {
				foreach (var party in parties.Where(p => p.IsEnabled && p.ElectionId.Equals(electionId, StringComparison.OrdinalIgnoreCase))) {
					if (!partyResults.Any(r => r.Identifier.Equals(party.PartyId, StringComparison.OrdinalIgnoreCase))) {
						yield return new ResultCsvRecord {
							ElectionId = electionId,
							ElectionLabel = electionLabel,
							PartyIdentifier = party.PartyId,
							PartyLabel = getPartyLabel(party),
							Votes = 0,
						};
					}
				}
			}

			// Yield blanks row if not already yielded

			if (!partyResults.Any(p => p.Identifier.Equals(PartyResult.BlankIdentifier, StringComparison.OrdinalIgnoreCase))) {
				yield return new ResultCsvRecord {
					ElectionId = electionId,
					ElectionLabel = electionLabel,
					PartyIdentifier = PartyResult.BlankIdentifier,
					PartyLabel = BlankVotesLabel,
					Votes = 0,
				};
			}

			// Yield nulls row if not already yielded

			if (!partyResults.Any(p => p.Identifier.Equals(PartyResult.NullIdentifier, StringComparison.OrdinalIgnoreCase))) {
				yield return new ResultCsvRecord {
					ElectionId = electionId,
					ElectionLabel = electionLabel,
					PartyIdentifier = PartyResult.NullIdentifier,
					PartyLabel = NullVotesLabel,
					Votes = nullifiedPartyResults.Sum(npr => npr.Votes),
				};
			}
		}

		private string getElectionLabel(ElectionResult electionResult) {
			var partyFromElection = parties?.FirstOrDefault(p => p.ElectionId.Equals(electionResult.Id, StringComparison.OrdinalIgnoreCase));
			return partyFromElection != null ? $"{partyFromElection.SubscriptionName} - {partyFromElection.ElectionName}" : null;
		}

		private string getPartyLabel(PartyResult partyResult) {

			if (partyResult.Identifier == PartyResult.BlankIdentifier) {
				return BlankVotesLabel;
			}
			
			if (partyResult.Identifier == PartyResult.NullIdentifier) {
				return NullVotesLabel;
			}

			var party = parties?.FirstOrDefault(p => p.PartyId.Equals(partyResult.Identifier, StringComparison.OrdinalIgnoreCase));

			return party != null ? getPartyLabel(party) : null;
		}

		private string getPartyLabel(PartyCsvRecord party) {

			var hasName = !string.IsNullOrEmpty(party.PartyName);
			var hasNumber = !string.IsNullOrEmpty(party.PartyNumber) && !party.PartyNumber.Equals("NULL", StringComparison.OrdinalIgnoreCase);

			return hasName && hasNumber ? $"{party.PartyNumber}. {party.PartyName}"
				: hasName ? party.PartyName
				: party.PartyNumber;
		}
	}
}
