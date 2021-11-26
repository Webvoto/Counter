using Counter.Classes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Counter.Entities {
	public struct Vote {

		public List<VoteModel> EncryptedChoices { get; }

		public byte[] Signature { get; }

		public byte[] ServerSignature { get; }

		public Vote(List<VoteModel> encryptedChoices, byte[] signature, byte[] serverSignature) {
			EncryptedChoices = encryptedChoices;
			Signature = signature;
			ServerSignature = serverSignature;
		}

		public byte[] EncodedEncryptedChoices
			=> Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(EncryptedChoices));
	}

	public class VoteSlot {

		public const int ValueMaxLen = 2500; // fits 7 votes encrypted and signed with 2048 bit keys (274 * 7 + 260 * 2 = 2438)
		private const int GuidByteLen = 16;
		private const int ShortIntegerLen = 2;

		public int PoolId { get; set; }

		public VotePool Pool { get; set; }

		public int Slot { get; set; }

		public bool HasValue { get; set; }

		[Required, MaxLength(ValueMaxLen)] // and HasFixedLength (configured via fluent API)
		public byte[] Value { get; set; }

		public Vote GetVote() {
			if (!HasValue) {
				throw new InvalidOperationException($"The vote slot #{Slot} of pool {PoolId} is not filled");
			}
			return decodeValue(this.Value);
		}

		#region Vote value encoding

		private static Vote decodeValue(byte[] encoded) {

			var offset = 0;

			// choices count
			var choicesCount = BitConverter.ToUInt16(encoded.AsSpan(offset, ShortIntegerLen));
			offset += ShortIntegerLen;

			// choices
			var encryptedChoices = new List<VoteModel>();
			for (var i = 0; i < choicesCount; i++) {

				// electionId
				var electionId = new Guid(encoded.AsSpan(offset, GuidByteLen));
				offset += GuidByteLen;
				// choiceLen
				var encryptedChoiceLen = BitConverter.ToUInt16(encoded.AsSpan(offset, ShortIntegerLen));
				offset += ShortIntegerLen;
				// choice
				var encryptedChoice = encoded.AsSpan(offset, encryptedChoiceLen).ToArray();
				offset += encryptedChoiceLen;

				encryptedChoices.Add(new VoteModel() { ElectionId = electionId, EncryptedChoice = encryptedChoice });
			}

			// signature len
			var signatureLen = BitConverter.ToUInt16(encoded.AsSpan(offset, ShortIntegerLen));
			offset += ShortIntegerLen;
			// signature
			var signature = encoded.AsSpan(offset, signatureLen).ToArray();
			offset += signatureLen;

			// server signature len
			var serverSignatureLen = BitConverter.ToUInt16(encoded.AsSpan(offset, ShortIntegerLen));
			offset += ShortIntegerLen;
			// server signature
			var serverSignature = encoded.AsSpan(offset, serverSignatureLen).ToArray();
			offset += serverSignatureLen;

			if (encoded.Skip(offset).Any(b => b != 0)) {
				throw new Exception("Inconsistent vote decoding");
			}

			return new Vote(encryptedChoices, signature, serverSignature);
		}

		#endregion
	}
}
