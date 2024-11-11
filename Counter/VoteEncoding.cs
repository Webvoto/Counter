using Org.BouncyCastle.Asn1;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Counter {

	public static class VoteEncoding {

		public static Asn1VoteChoice Decode(byte[] encoded) => new Asn1VoteChoice(new Asn1InputStream(encoded).ReadObject());
	}

	public class Asn1VoteChoice {
		public string ElectionId{ get; }

		public byte[] EncryptedChoice { get; }

		public Asn1VoteChoice(Asn1Encodable asn1Object) {
			var seq = (Asn1Sequence)asn1Object;
			ElectionId = ((DerPrintableString)seq[0]).GetString();
			EncryptedChoice = ((DerOctetString)seq[1]).GetOctets();
		}
	}
}
