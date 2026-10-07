using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class TeamMemberRepo : DatabaseRepo<TeamMember> , ITeamMemberRepo
	{
		public TeamMember GetByUserId(string userId)
		{
			var filter = Builders<TeamMember>.Filter.Eq(t => t.UserId, userId);
			return DBCollection.Find(filter).FirstOrDefault();
		}
	}
}
