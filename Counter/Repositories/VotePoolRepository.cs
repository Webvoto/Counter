using Counter.Database;
using Counter.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Counter.Repositories {
	public class VotePoolRepository {
		private readonly AppDbContext context;

		public VotePoolRepository(AppDbContext context) {
			this.context = context;
		}

		public IQueryable<VotePool> VotePools => context.VotePools.OrderBy(vp => vp.Id).Where(vp => vp.Slots.Any(v => v.HasValue));

		public Task<int> CountVotePoolsAsync()
			=> VotePools.CountAsync();

		public Task<List<VotePool>> GetVotePoolsPaginatedAsync(int offset, int count)
			=> VotePools.Skip(offset).Take(count).ToListAsync();
	}
}
