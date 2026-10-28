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

		public List<TeamMember> GetByIds(IEnumerable<string> ids)
		{
			var filter = Builders<TeamMember>.Filter.In(t => t.Id, ValidIds(ids));
			return DBCollection.Find(filter).SortBy(t => t.Id).ToList();
		}

		// AnyEq: the AssignedRoles list contains the id. AssignedRoles holds plain
		// text, not ObjectIds, so no id check is needed - a bad id just matches nothing.
		public long CountWithRole(string projectRoleId)
		{
			var filter = Builders<TeamMember>.Filter.AnyEq(t => t.AssignedRoles, projectRoleId);
			return DBCollection.CountDocuments(filter);
		}

		public List<TeamMember> GetStaff()
		{
			var filter = Builders<TeamMember>.Filter.Ne(t => t.UserId, null);
			var nameOnly = Builders<TeamMember>.Projection
				.Include(t => t.Name)
				.Include(t => t.UserId);
			return DBCollection.Find(filter).Project<TeamMember>(nameOnly).ToList();
		}
	}
}
