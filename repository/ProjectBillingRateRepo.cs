using MongoDB.Driver;
using System.Collections.Generic;

namespace sma.plan
{
	internal class ProjectBillingRateRepo : DatabaseRepo<ProjectBillingRate>, IProjectBillingRateRepo
	{
		public List<ProjectBillingRate> GetByProject(string projectId)
		{
			var filter = Builders<ProjectBillingRate>.Filter.Eq(r => r.ProjectId, projectId);
			return DBCollection.Find(filter).ToList();
		}

		public List<ProjectBillingRate> GetByProjects(IEnumerable<string> projectIds)
		{
			var filter = Builders<ProjectBillingRate>.Filter.In(r => r.ProjectId, ValidIds(projectIds));
			return DBCollection.Find(filter).ToList();
		}

		public long CountByRole(string projectRoleId)
		{
			var filter = Builders<ProjectBillingRate>.Filter.In(r => r.ProjectRoleId, ValidIds(new[] { projectRoleId }));
			return DBCollection.CountDocuments(filter);
		}
	}
}
