using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Counter.Entities {
	public class ElectionKey {

		public const string VoteEncryptionPurpose = "VOTE_ENC";
		public const string VoteSignaturePurpose = "VOTE_SIG";

		[Key]
		public Guid Id { get; set; }

		[Required, MaxLength(8)]
		public string PurposeCode { get; set; }

		public byte[] PublicKey { get; set; }

		public byte[] Certificate { get; set; }
	}
}
