using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Counter.Classes {
	public class VoteModel {

		public Guid ElectionId { get; set; }

		public byte[] EncryptedChoice { get; set; }
	}
}
