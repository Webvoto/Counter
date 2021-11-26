using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Counter.Entities {
	public class Party {

		[Key]
		public Guid Id { get; set; }

		public Guid ElectionId { get; set; }

		public Election Election { get; set; }

		[MaxLength(100)]
		public string Name { get; set; }

		public int? Number { get; set; }

		public int? Order { get; set; }

		public int? Priority { get; set; }

		[NotMapped]
		public int OrderOrNumber => Order ?? Number ?? 1_000_000;

		[NotMapped]
		public int PriorityScore => (Priority ?? 999) * 1_000_000 + (Order ?? 999) * 1_000 + (Number ?? 999);
	}
}
