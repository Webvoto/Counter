using Org.BouncyCastle.Asn1;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Counter {

	public static class VoteEncoding {

		public static Asn1Vote Decode(byte[] encoded) => new Asn1Vote(new Asn1InputStream(encoded).ReadObject());
	}

	public class Asn1Vote {

		public Asn1Vote(Asn1Encodable asn1Object) {
			var seq = (Asn1Sequence)asn1Object;
			Choices = new Asn1VoteChoiceList(seq[0]);
			PoolId = ((DerInteger)seq[1]).Value.IntValue;
			Slot = ((DerInteger)seq[2]).Value.IntValue;
		}

		public Asn1VoteChoiceList Choices { get; }

		public int PoolId { get; }

		public int Slot { get; }
	}

	public class Asn1VoteChoiceList : List<Asn1VoteChoice> {

		public Asn1VoteChoiceList(Asn1Encodable asn1Object) : base(decode(asn1Object)) {
		}

		private static IEnumerable<Asn1VoteChoice> decode(Asn1Encodable asn1Object) {
			var seq = (Asn1Sequence)asn1Object;
			var choices = new List<Asn1VoteChoice>();
			foreach (var item in seq) {
				choices.Add(new Asn1VoteChoice(item));
			}
			return choices;
		}
	}

	public class Asn1VoteChoice {

		public string ElectionId { get; }

		public string DistrictId { get; }

		public byte[] EncryptedChoice { get; }

		public Asn1VoteChoice(Asn1Encodable asn1Object) {
			var seq = (Asn1Sequence)asn1Object;
			ElectionId = ((DerPrintableString)seq[0]).GetString();
			DistrictId = ((DerPrintableString)seq[1]).GetString();
			EncryptedChoice = ((DerOctetString)seq[2]).GetOctets();
		}
	}
}
