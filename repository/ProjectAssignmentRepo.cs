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

		public List<ProjectAssignment> GetByProjects(IEnumerable<string> projectIds)
		{
			var filter = Builders<ProjectAssignment>.Filter.In(a => a.ProjectId, ValidIds(projectIds));
			return DBCollection.Find(filter).ToList();
		}

		public List<ProjectAssignment> GetByTeamMember(string teamMemberId)
		{
			var filter = Builders<ProjectAssignment>.Filter.Eq(a => a.TeamMemberId, teamMemberId);
			return DBCollection.Find(filter).ToList();
		}

		public long CountByRole(string projectRoleId)
		{
			var filter = Builders<ProjectAssignment>.Filter.In(a => a.ProjectRoleId, ValidIds(new[] { projectRoleId }));
			return DBCollection.CountDocuments(filter);
		}

		// Counted by the database, stopping at the first: nothing is loaded.
		public bool IsAssigned(string teamMemberId, string projectId)
		{
			if (ValidIds(new[] { teamMemberId, projectId }).Count < 2)
			{
				return false;
			}
			var filter = Builders<ProjectAssignment>.Filter.Eq(a => a.TeamMemberId, teamMemberId)
				& Builders<ProjectAssignment>.Filter.Eq(a => a.ProjectId, projectId);
			return DBCollection.CountDocuments(filter, new CountOptions { Limit = 1 }) > 0;
		}
	}
}
