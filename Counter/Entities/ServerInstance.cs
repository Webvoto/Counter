using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Counter.Entities {
	public class ServerInstance {

		[Key]
		public int Id { get; set; }

		public byte[] PublicKey { get; set; }
	}
}
