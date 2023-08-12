using Org.BouncyCastle.Asn1;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Counter {

	public static class VoteEncoding {

		public static EncodedVote Decode(byte[] encoded) => new EncodedVote(new Asn1InputStream(encoded).ReadObject());
	}

	public struct VoteItem {

		public Guid ElectionId { get; }

		public Guid? DistrictId { get; }

		public byte[] EncryptedChoice { get; }

		public VoteItem(Guid electionId, Guid? districtId, byte[] encryptedChoice) {
			ElectionId = electionId;
			DistrictId = districtId;
			EncryptedChoice = encryptedChoice;
		}
	}

	//[Asn1Sequence]
	public class EncodedVote {
		[Obsolete($"This empty constructor should not be used", error: true)]
		public EncodedVote() { }

		public EncodedVote(int poolId, int slot, IEnumerable<EncodedVoteChoice> choices) {
			PoolId = poolId;
			Slot = slot;
			Choices = new EncodedVoteChoiceList(choices);
		}

		public EncodedVote(int poolId, int slot, IEnumerable<VoteItem> choices) {
			PoolId = poolId;
			Slot = slot;
			Choices = new EncodedVoteChoiceList(choices);
		}

		public EncodedVote(Asn1Encodable asn1Object) {
			var seq = (Asn1Sequence)asn1Object;
			Choices = new EncodedVoteChoiceList(seq[0]);
			PoolId = ((DerInteger)seq[1]).Value.IntValue;
			Slot = ((DerInteger)seq[2]).Value.IntValue;
		}

		//[Asn1SequenceElement(0)]
		public EncodedVoteChoiceList Choices { get; set; }

		//[Asn1SequenceElement(1, Asn1PrimitiveTypes.Integer)]
		public int PoolId { get; set; }

		//[Asn1SequenceElement(2, Asn1PrimitiveTypes.Integer)]
		public int Slot { get; set; }

		public Asn1Encodable ToAsn1() => new DerSequence(
			Choices.ToAsn1(),
			new DerInteger(PoolId),
			new DerInteger(Slot)
		);
	}

	//[Asn1SequenceOf(typeof(EncodedVoteChoice))]
	public class EncodedVoteChoiceList : List<EncodedVoteChoice> {
		public EncodedVoteChoiceList() {
		}

		public EncodedVoteChoiceList(IEnumerable<EncodedVoteChoice> choices) : base(choices) {
		}

		public EncodedVoteChoiceList(IEnumerable<VoteItem> choices) : base(choices.Select(c => new EncodedVoteChoice(c))) {
		}

		public EncodedVoteChoiceList(Asn1Encodable asn1Object) : this(decode(asn1Object)) {
		}

		private static IEnumerable<EncodedVoteChoice> decode(Asn1Encodable asn1Object) {
			var seq = (Asn1Sequence)asn1Object;
			var choices = new List<EncodedVoteChoice>();
			foreach (var item in seq) {
				choices.Add(new EncodedVoteChoice(item));
			}
			return choices;
		}

		public Asn1Encodable ToAsn1() => new DerSequence(this.Select(c => c.ToAsn1()).ToArray());
	}

	//[Asn1Sequence]
	public class EncodedVoteChoice {

		//[Asn1SequenceElement(0, Asn1PrimitiveTypes.PrintableString)]
		public string ElectionId { get; set; }

		//[Asn1SequenceElement(1, Asn1PrimitiveTypes.PrintableString)]
		public string DistrictId { get; set; }

		//[Asn1SequenceElement(2, Asn1PrimitiveTypes.OctetString)]
		public byte[] EncryptedChoice { get; set; }

		public EncodedVoteChoice() {
		}

		public EncodedVoteChoice(VoteItem model) {
			ElectionId = model.ElectionId.ToString();
			DistrictId = model.DistrictId?.ToString() ?? string.Empty; // Asn1Util does not support nulls
			EncryptedChoice = model.EncryptedChoice;
		}

		public EncodedVoteChoice(Asn1Encodable asn1Object) {
			var seq = (Asn1Sequence)asn1Object;
			ElectionId = ((DerPrintableString)seq[0]).GetString();
			DistrictId = ((DerPrintableString)seq[1]).GetString();
			EncryptedChoice = ((DerOctetString)seq[2]).GetOctets();
		}

		public VoteItem ToModel() => new VoteItem(
			new Guid(ElectionId),
			!string.IsNullOrEmpty(DistrictId) ? new Guid(DistrictId) : null,
			EncryptedChoice
		);

		public Asn1Encodable ToAsn1() => new DerSequence(
			new DerPrintableString(ElectionId),
			new DerPrintableString(DistrictId),
			new DerOctetString(EncryptedChoice)
		);
	}
}
