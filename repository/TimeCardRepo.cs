using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class TimeCardRepo : DatabaseRepo<TimeCard>, ITimeCardRepo
	{


		public async Task<List<TimeCard>> GetByTeamMember(string teamMemberId, DateOnly cutoffDate)
		{

			cutoffDate = cutoffDate == null ? DateOnly.MinValue : cutoffDate;
			var idFilter = Builders<TimeCard>.Filter.Eq(p => p.TeamMemberId, teamMemberId);
			var dateFilter = Builders<TimeCard>.Filter.Gt(p => p.Month, cutoffDate);
			var result = await DBCollection.Find(idFilter & dateFilter).ToListAsync();
			return result;
		}

	}
}
