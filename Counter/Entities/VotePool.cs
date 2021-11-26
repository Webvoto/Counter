using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Counter.Entities {
	public class VotePool {
		[Key]
		public int Id { get; set; }

		public int? ServerInstanceId { get; set; }

		public Guid? VoteBoxId { get; set; }

		public List<VoteSlot> Slots { get; set; }
	}
}
