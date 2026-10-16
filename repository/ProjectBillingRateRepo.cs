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
	}
}
