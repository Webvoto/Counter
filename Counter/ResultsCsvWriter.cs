using CsvHelper;
using CsvHelper.Configuration;
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

		public string DistrictId { get; set; }

		public string DistrictLabel { get; set; }

		public string PartyIdentifier { get; set; }

		public string PartyLabel { get; set; }

		public int Votes { get; set; }
	}

	public class ResultsCsvWriter {

		private const string BlankVotesLabel = "Votos brancos";
		private const string NullVotesLabel = "Votos nulos";

		private readonly List<PartyCsvRecord> parties;
		private readonly List<DistrictCsvRecord> districts;

		public ResultsCsvWriter(List<PartyCsvRecord> parties, List<DistrictCsvRecord> districts) {
			this.parties = parties;
			this.districts = districts;
		}

		public void Write(ElectionResultCollection results, Stream outStream) {

			var records = new List<ResultCsvRecord>();

			foreach (var electionResult in results.ElectionResults) {
				var electionLabel = getElectionLabel(electionResult);
				foreach (var districtResult in electionResult.DistrictResults) {
					records.AddRange(getDistrictRecords(electionResult.Id, electionLabel, districtResult));
				}
			}

			var orderedRecords = records
				.OrderBy(r => r.ElectionLabel)
				.ThenBy(r => r.DistrictLabel)
				.ThenBy(r => r.PartyLabel == BlankVotesLabel || r.PartyLabel == NullVotesLabel ? 1 : 0)
				.ThenByDescending(r => r.Votes);

			using var streamWriter = new StreamWriter(outStream, Encoding.UTF8);
			using var csvWriter = new CsvWriter(streamWriter, Thread.CurrentThread.CurrentCulture);
			csvWriter.WriteRecords(orderedRecords);
		}

		private IEnumerable<ResultCsvRecord> getDistrictRecords(string electionId, string electionLabel, DistrictResult districtResult) {
			
			var districtLabel = getDistrictLabel(districtResult);

			var nullifiedPartyResults = new List<PartyResult>();

			foreach (var partyResult in districtResult.PartyResults) {
				if (!partyResult.IsBlankOrNull) {
					var party = parties?.FirstOrDefault(p => p.PartyId.Equals(partyResult.Identifier, StringComparison.OrdinalIgnoreCase));
					if (party != null && !party.IsEnabled) {
						nullifiedPartyResults.Add(partyResult);
					}
				}
			}

			foreach (var partyResult in districtResult.PartyResults.Except(nullifiedPartyResults)) {
				yield return new ResultCsvRecord {
					ElectionId = electionId,
					ElectionLabel = electionLabel,
					DistrictId = districtResult.Id,
					DistrictLabel = districtLabel,
					PartyIdentifier = partyResult.Identifier,
					PartyLabel = getPartyLabel(partyResult),
					Votes = partyResult.Votes + (partyResult.IsNull ? nullifiedPartyResults.Sum(npr => npr.Votes) : 0),
				};
			}

			if (parties != null) {
				foreach (var party in parties.Where(p => p.IsEnabled && p.ElectionId.Equals(electionId, StringComparison.OrdinalIgnoreCase))) {
					if (!districtResult.PartyResults.Any(r => r.Identifier.Equals(party.PartyId, StringComparison.OrdinalIgnoreCase))) {
						yield return new ResultCsvRecord {
							ElectionId = electionId,
							ElectionLabel = electionLabel,
							DistrictId = districtResult.Id,
							DistrictLabel = districtLabel,
							PartyIdentifier = party.PartyId,
							PartyLabel = getPartyLabel(party),
							Votes = 0,
						};
					}
				}
			}

			if (!districtResult.PartyResults.Any(p => p.Identifier.Equals(PartyResult.BlankIdentifier, StringComparison.OrdinalIgnoreCase))) {
				yield return new ResultCsvRecord {
					ElectionId = electionId,
					ElectionLabel = electionLabel,
					DistrictId = districtResult.Id,
					DistrictLabel = districtLabel,
					PartyIdentifier = PartyResult.BlankIdentifier,
					PartyLabel = BlankVotesLabel,
					Votes = 0,
				};
			}

			if (!districtResult.PartyResults.Any(p => p.Identifier.Equals(PartyResult.NullIdentifier, StringComparison.OrdinalIgnoreCase))) {
				yield return new ResultCsvRecord {
					ElectionId = electionId,
					ElectionLabel = electionLabel,
					DistrictId = districtResult.Id,
					DistrictLabel = districtLabel,
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

		private string getDistrictLabel(DistrictResult districtResult)
			=> districts?.FirstOrDefault(d => d.DistrictId.Equals(districtResult.Id, StringComparison.OrdinalIgnoreCase))?.DistrictName ?? "(não especificado)";

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
