using Org.BouncyCastle.Asn1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Counter {

	public static class VoteEncoding {

		private const int EncryptedCekLength = 256; // RSA 2048 bits cryptogram
		private const int ContentEncryptionBlockLength = 16; // AES block length

		private static readonly Guid NullChoice = new("ffffffff-ffff-ffff-ffff-ffffffffffff");
		private static readonly Guid BlankChoice = new("00000000-0000-0000-0000-000000000000");

		public static Asn1Vote Decode(byte[] encoded) => new Asn1Vote(new Asn1InputStream(encoded).ReadObject());

		/*
		 * Encrypted choices formats:
		 *
		 * - Legacy: a single choice encrypted with the election key (2048-bit RSA-OAEP SHA-256, 256 bytes). The plaintext is
		 *   the party identifier in UTF-8.
		 * - Current (Election since April 2026, copied from its Business/Classes/VoteEncoding.cs): the CEK (AES-256) encrypted
		 *   with the election key (256 bytes), the CEIV (16 bytes), then the choices encrypted with the CEK (AES-256-CBC,
		 *   PKCS#7). Each choice is a 16-byte GUID: the party, zeros for a blank vote or all ff for a null vote.
		 *
		 * Either way, the first 256 bytes are what the election key decrypts.
		 */

		public static byte[] GetEncryptedKey(byte[] encryptedChoices)
			=> encryptedChoices.Take(EncryptedCekLength).ToArray();

		// The decrypted key is the choice itself in the legacy format, or the CEK in the current one
		public static List<string> DecryptChoices(byte[] encryptedChoices, byte[] decryptedKey) {

			if (encryptedChoices.Length == EncryptedCekLength) {
				return [Encoding.UTF8.GetString(decryptedKey)];
			}

			var encryptedContentLength = encryptedChoices.Length - EncryptedCekLength - ContentEncryptionBlockLength;
			if (encryptedContentLength <= 0 || encryptedContentLength % ContentEncryptionBlockLength != 0) {
				throw new Exception($"Encrypted choices with an inconsistent length: {encryptedChoices.Length}");
			}
			var ceiv = encryptedChoices.Skip(EncryptedCekLength).Take(ContentEncryptionBlockLength).ToArray();
			var encryptedContent = encryptedChoices.Skip(EncryptedCekLength + ContentEncryptionBlockLength).ToArray();

			using var aes = Aes.Create();
			aes.Key = decryptedKey;
			var encodedChoices = aes.DecryptCbc(encryptedContent, ceiv, PaddingMode.PKCS7);

			if (encodedChoices.Length == 0 || encodedChoices.Length % 16 != 0) {
				throw new Exception($"Bad encoded choices length: {encodedChoices.Length}");
			}
			var choices = new List<string>();
			for (var i = 0; i < encodedChoices.Length; i += 16) {
				var choiceId = new Guid(encodedChoices.AsSpan(i, 16), bigEndian: true);
				choices.Add(choiceId == BlankChoice ? PartyResult.BlankIdentifier : choiceId == NullChoice ? PartyResult.NullIdentifier : choiceId.ToString());
			}
			return choices;
		}
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

		public byte[] EncryptedChoices { get; }

		// Absent in votes from before the vote weight was added
		public int VoteWeight { get; }

		public Asn1VoteChoice(Asn1Encodable asn1Object) {
			var seq = (Asn1Sequence)asn1Object;
			ElectionId = ((DerPrintableString)seq[0]).GetString();
			DistrictId = ((DerPrintableString)seq[1]).GetString();
			EncryptedChoices = ((DerOctetString)seq[2]).GetOctets();
			VoteWeight = seq.Count > 3 ? ((DerInteger)seq[3]).Value.IntValue : 1;
		}
	}
}
