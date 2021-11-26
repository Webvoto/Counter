using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Counter.Entities {
	public class Election {
		[Key]
		public Guid Id { get; set; }

		public Guid OfficeId { get; set; }

		public Office Office { get; set; }
	}
}
