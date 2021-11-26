using Counter.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Counter.Database {
	public class AppDbContext : DbContext {
		public AppDbContext(DbContextOptions options) : base(options) {
		}

		protected override void OnModelCreating(ModelBuilder modelBuilder) {
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<VoteSlot>().HasKey(v => new { v.PoolId, v.Slot });
		}

		public DbSet<VotePool> VotePools { get; set; }
		public DbSet<VoteSlot> VoteSlots { get; set;  }
		public DbSet<ServerInstance> ServerInstances { get; set; }
		public DbSet<ElectionKey> ElectionKeys { get; set; }
		public DbSet<Election> Elections { get; set; }
		public DbSet<Office> Offices { get; set; }
		public DbSet<Party> Parties { get; set; }
	}
}
