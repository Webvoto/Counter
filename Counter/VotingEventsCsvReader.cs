using CsvHelper;
using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Counter {

	public class VotingEventCsvRecord {

		// Utils
		public int ServerInstanceId { get; set; }

		// V1
		public string Id { get; set; }
		
		public DateTime DateUtc { get; set; }
		
		public string TypeCode { get; set; }
		
		public string SubscriptionId { get; set; }
		
		public string SessionId { get; set; }
		
		public string QuestionId { get; set; }
		
		public string VoterId { get; set; }
		
		public string MemberId { get; set; }
		
		public string AgentId { get; set; }
		
		public string VotingChannelCode { get; set; }
		
		public string RemoteIP { get; set; }
		
		public string RemotePort { get; set; }
		
		public string AzureRef { get; set; }
		
		public string UserAgentString { get; set; }
		
		public string IdentifierKindCode { get; set; }
		
		public string Identifier { get; set; }
		
		public string DelegateVoterId { get; set; }
		
		public string VoterOtpId { get; set; }
		
		public string BioSessionId { get; set; }
		
		public string BioAuthenticationFailureCode { get; set; }
		
		public string BioEnrollmentFailureCode { get; set; }
		
		public string CertificateTypeCode { get; set; }
		
		public string CloudCertificateAuthenticationFailureCode { get; set; }
		
		public string AuthServerAuthenticationFailureCode { get; set; }
		
		public string WebPkiAuthenticationFailureCode { get; set; }
		
		public string OtpCheckFailureCode { get; set; }
		
		public string CertificateId { get; set; }

		public string ValidationResultsBlobId { get; set; }
		
		public string VoterContactId { get; set; }
		
		public string SubmitVoteFailureCode { get; set; }
		
		public string CausedVoterLock { get; set; }

		// V2
		public string ServerSignature { get; set; }

		public string LogNumber { get; set; }

		public string Sequence { get; set; }

		public string WorkerId { get; set; }

		public string VoteBoxId { get; set; }

		public string Details { get; set; }

		// V3
		public string ChainedLogId { get; set; }

		public string PasswordCheckFailureCode { get; set; }

		public string PasswordId { get; set; }

		public string CampaignNotificationId { get; set; }

		// V4
		public string VoterAddressId { get; set; }

	}

	public class VotingEventsCsvReader : IDisposable {

		private readonly Stream stream;
		private readonly StreamReader streamReader;
		private readonly CsvReader csvReader;

		public static VotingEventsCsvReader Open(FileInfo file) {
			var stream = file.OpenRead();
			var streamReader = new StreamReader(stream);
			var useInvariantCulture = bool.TryParse(Environment.GetEnvironmentVariable("USE_INVARIANT_CULTURE_FOR_VOTING_EVENTS_CSV"), out var b) && b;
			var csvReader = new CsvReader(streamReader, useInvariantCulture ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture);
			return new VotingEventsCsvReader(stream, streamReader, csvReader);
		}

		private VotingEventsCsvReader(Stream stream, StreamReader streamReader, CsvReader csvReader) {
			this.stream = stream;
			this.streamReader = streamReader;
			this.csvReader = csvReader;
		}

		public IEnumerable<VotingEventCsvRecord> GetRecords()
			=> csvReader.GetRecords<VotingEventCsvRecord>();

		public void Dispose() {
			csvReader.Dispose();
			streamReader.Dispose();
			stream.Dispose();
		}
	}
}
