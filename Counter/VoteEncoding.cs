using Org.BouncyCastle.Asn1;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Counter {

	public record VoteValue(int PoolId, int SlotNumber, Guid QuestionId, Guid MemberDistrictId, Guid? VoteBoxId, decimal Shares, Guid VoteEncryptionKeyVersionId, byte[] EncryptedChoices);

	public static class VoteEncoding {

		public static VoteValue Decode(byte[] encoded) {
			var encodedVote = new EncodedVote(new Asn1InputStream(encoded).ReadObject());
			return new VoteValue(
				encodedVote.PoolId,
				encodedVote.SlotNumber,
				new Guid(encodedVote.QuestionId),
				new Guid(encodedVote.MemberDistrictId),
				!string.IsNullOrEmpty(encodedVote.VoteBoxId) ? new Guid(encodedVote.VoteBoxId) : null,
				decimal.Parse(encodedVote.Shares, CultureInfo.InvariantCulture),
				new Guid(encodedVote.VoteEncryptionKeyVersionId),
				encodedVote.EncryptedChoices
			);
		}

		// [Asn1Sequence]
		private class EncodedVote {

			// [Asn1SequenceElement(0, Asn1PrimitiveTypes.Integer)]
			public int PoolId { get; set; }

			// [Asn1SequenceElement(1, Asn1PrimitiveTypes.Integer)]
			public int SlotNumber { get; set; }

			// [Asn1SequenceElement(2, Asn1PrimitiveTypes.PrintableString)]
			public string QuestionId { get; set; }

			// [Asn1SequenceElement(3, Asn1PrimitiveTypes.PrintableString)]
			public string MemberDistrictId { get; set; }

			// [Asn1SequenceElement(4, Asn1PrimitiveTypes.PrintableString)]
			public string VoteBoxId { get; set; }

			// [Asn1SequenceElement(5, Asn1PrimitiveTypes.PrintableString)]
			public string Shares { get; set; }

			// [Asn1SequenceElement(6, Asn1PrimitiveTypes.PrintableString)]
			public string VoteEncryptionKeyVersionId { get; set; }

			// [Asn1SequenceElement(7, Asn1PrimitiveTypes.OctetString)]
			public byte[] EncryptedChoices { get; set; }

			public EncodedVote(Asn1Encodable asn1Object) {
				var seq = (Asn1Sequence)asn1Object;
				PoolId = ((DerInteger)seq[0]).Value.IntValue;
				SlotNumber = ((DerInteger)seq[1]).Value.IntValue;
				QuestionId = ((DerPrintableString)seq[2]).GetString();
				MemberDistrictId = ((DerPrintableString)seq[3]).GetString();
				VoteBoxId = ((DerPrintableString)seq[4]).GetString();
				Shares = ((DerPrintableString)seq[5]).GetString();
				VoteEncryptionKeyVersionId = ((DerPrintableString)seq[6]).GetString();
				EncryptedChoices = ((DerOctetString)seq[7]).GetOctets();
			}
		}

		public static List<Guid> DecodeChoices(ReadOnlySpan<byte> encodedChoices) {

			if (encodedChoices.Length % 16 != 0) {
				throw new Exception($"Bad encoded choices length: {encodedChoices.Length}");
			}

			var chosenOptionIds = new List<Guid>();

			for (var i = 0; i < encodedChoices.Length; i += 16) {
				chosenOptionIds.Add(new Guid(encodedChoices.Slice(i, 16), true));
			}

			return chosenOptionIds;
		}
	}
}
