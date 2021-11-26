using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Counter.Entities {
	public class Office {
		public const int MaxNameSize = 50;

		[Key]
		public Guid Id { get; set; }

		[Required, MaxLength(MaxNameSize)]
		public string Name { get; set; }

		public int Order { get; set; }
	}
}
