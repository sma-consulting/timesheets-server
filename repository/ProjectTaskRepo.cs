
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class ProjectTaskRepo : DatabaseRepo<ProjectTask>, IProjectTaskRepo
	{
		public List<ProjectTask> GetByIds(IEnumerable<string> ids)
		{
			var filter = Builders<ProjectTask>.Filter.In(t => t.Id, ValidIds(ids));
			return DBCollection.Find(filter).SortBy(t => t.Id).ToList();
		}

		public List<ProjectTask> GetByProjects(IEnumerable<string> projectIds)
		{
			var filter = Builders<ProjectTask>.Filter.In(t => t.ProjectId, ValidIds(projectIds));
			return DBCollection.Find(filter).SortBy(t => t.Id).ToList();
		}
	}
}
