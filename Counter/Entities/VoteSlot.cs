using Counter.Classes;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Counter.Entities
{
	public struct Vote
	{
		public int PoolId { get; }

		public int Slot { get; }

		public List<VoteItem> EncryptedChoices { get; }

		public byte[] Signature { get; }

		public byte[] ServerSignature { get; }

		public Vote(int poolId, int slot, List<VoteItem> encryptedChoices, byte[] signature, byte[] serverSignature)
		{
			PoolId = poolId;
			Slot = slot;
			EncryptedChoices = encryptedChoices;

			Signature = signature;
			ServerSignature = serverSignature;
		}
	}

	public struct VoteItem
	{

		public Guid ElectionId { get; }

		public Guid? DistrictId { get; }

		public byte[] EncryptedChoice { get; }

		public VoteItem(Guid electionId, Guid? districtId, byte[] encryptedChoice)
		{
			ElectionId = electionId;
			DistrictId = districtId;
			EncryptedChoice = encryptedChoice;
		}
	}

	public class VoteSlot
	{

		public const int ValueMaxLen = 2500; // fits 7 votes encrypted and signed with 2048 bit keys (274 * 7 + 260 * 2 = 2438)

		public int PoolId { get; set; }

		public VotePool Pool { get; set; }

		public int Slot { get; set; }

		public bool HasValue { get; set; }

		[Required, MaxLength(ValueMaxLen)] // and HasFixedLength (configured via fluent API)
		public byte[] Value { get; set; }

		public Vote GetVote()
		{
			if (!HasValue)
			{
				throw new InvalidOperationException($"The vote slot #{Slot} of pool {PoolId} is not filled");
			}
			return decodeValue(this.Value);
		}

		public static Vote decodeValue(byte[] encoded)
		{
			var asn1Stream = new Asn1InputStream(encoded);
			var encodedVote = new EncodedVote(asn1Stream.ReadObject());
			return encodedVote.ToModel();
		}
	}


	#region Vote encoding

	//[Asn1Sequence]
	public class EncodedVote
	{

		//[Asn1SequenceElement(0)]
		public EncodedVoteContent Content { get; set; }

		//[Asn1SequenceElement(1, Asn1PrimitiveTypes.OctetString)]
		public byte[] Signature { get; set; }

		//[Asn1SequenceElement(2, Asn1PrimitiveTypes.OctetString)]
		public byte[] ServerSignature { get; set; }

		public EncodedVote()
		{
		}

		public EncodedVote(Vote vote)
		{
			Content = new EncodedVoteContent(
				vote.PoolId,
				vote.Slot,
				vote.EncryptedChoices.Select(c => new EncodedVoteChoice(c))
			);

			Signature = vote.Signature;
			ServerSignature = vote.ServerSignature;
		}

		public EncodedVote(Asn1Encodable asn1Object)
		{
			var seq = (Asn1Sequence)asn1Object;
			Content = new EncodedVoteContent(seq[0]);
			Signature = ((DerOctetString)seq[1]).GetOctets();
			ServerSignature = ((DerOctetString)seq[2]).GetOctets();
		}

		public Vote ToModel() => new Vote(
			Content.PoolId,
			Content.Slot,
			Content.Choices.ConvertAll(c => c.ToModel()),
			Signature,
			ServerSignature);
	}

	//[Asn1Sequence]
	public class EncodedVoteContent
	{
		[Obsolete($"This empty constructor should not be used", error: true)]
		public EncodedVoteContent() { }

		public EncodedVoteContent(int poolId, int slot, IEnumerable<EncodedVoteChoice> choices)
		{
			PoolId = poolId;
			Slot = slot;
			Choices = new EncodedVoteChoiceList(choices);
		}

		public EncodedVoteContent(int poolId, int slot, IEnumerable<VoteItem> choices)
		{
			PoolId = poolId;
			Slot = slot;
			Choices = new EncodedVoteChoiceList(choices);
		}

		public EncodedVoteContent(Asn1Encodable asn1Object)
		{
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
	public class EncodedVoteChoiceList : List<EncodedVoteChoice>
	{
		public EncodedVoteChoiceList()
		{
		}

		public EncodedVoteChoiceList(IEnumerable<EncodedVoteChoice> choices) : base(choices)
		{
		}

		public EncodedVoteChoiceList(IEnumerable<VoteItem> choices) : base(choices.Select(c => new EncodedVoteChoice(c)))
		{
		}

		public EncodedVoteChoiceList(Asn1Encodable asn1Object): this(decode(asn1Object))
		{
		}

		private static IEnumerable<EncodedVoteChoice> decode(Asn1Encodable asn1Object)
		{
			var seq = (Asn1Sequence)asn1Object;
			var choices = new List<EncodedVoteChoice>();
			foreach (var item in seq)
			{
				choices.Add(new EncodedVoteChoice(item));
			}
			return choices;
		}

		public Asn1Encodable ToAsn1() => new DerSequence(this.Select(c => c.ToAsn1()).ToArray());
	}

	//[Asn1Sequence]
	public class EncodedVoteChoice
	{

		//[Asn1SequenceElement(0, Asn1PrimitiveTypes.PrintableString)]
		public string ElectionId { get; set; }

		//[Asn1SequenceElement(1, Asn1PrimitiveTypes.PrintableString)]
		public string DistrictId { get; set; }

		//[Asn1SequenceElement(2, Asn1PrimitiveTypes.OctetString)]
		public byte[] EncryptedChoice { get; set; }

		public EncodedVoteChoice()
		{
		}

		public EncodedVoteChoice(VoteItem model)
		{
			ElectionId = model.ElectionId.ToString();
			DistrictId = model.DistrictId?.ToString() ?? string.Empty; // Asn1Util does not support nulls
			EncryptedChoice = model.EncryptedChoice;
		}

		public EncodedVoteChoice(Asn1Encodable asn1Object)
		{
			var seq = (Asn1Sequence)asn1Object;
			ElectionId = ((DerPrintableString)seq[0]).GetString();
			DistrictId = ((DerPrintableString)seq[1]).GetString();
			EncryptedChoice = ((DerOctetString)seq[2]).GetOctets();
		}

		public VoteItem ToModel() => new VoteItem(
			new Guid(ElectionId),
			!string.IsNullOrEmpty(DistrictId) ? new Guid(DistrictId) : (Guid?)null,
			EncryptedChoice
		);

		public Asn1Encodable ToAsn1() => new DerSequence(
			new DerPrintableString(ElectionId),
			new DerPrintableString(DistrictId),
			new DerOctetString(EncryptedChoice)
		);
	}

	#endregion
}
