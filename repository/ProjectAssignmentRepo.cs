using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class ProjectAssignmentRepo : DatabaseRepo<ProjectAssignment>, IProjectAssignmentRepo
	{
		public List<ProjectAssignment> GetByProject(string projectId)
		{
			var filter = Builders<ProjectAssignment>.Filter.Eq(a => a.ProjectId, projectId);
			return DBCollection.Find(filter).ToList();
		}

		public List<ProjectAssignment> GetByTeamMember(string teamMemberId)
		{
			var filter = Builders<ProjectAssignment>.Filter.Eq(a => a.TeamMemberId, teamMemberId);
			return DBCollection.Find(filter).ToList();
		}
	}
}
