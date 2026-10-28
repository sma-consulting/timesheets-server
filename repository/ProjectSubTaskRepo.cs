
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class ProjectSubTaskRepo : DatabaseRepo<ProjectSubTask>, IProjectSubTaskRepo
	{
		public List<ProjectSubTask> GetByIds(IEnumerable<string> ids)
		{
			var filter = Builders<ProjectSubTask>.Filter.In(s => s.Id, ValidIds(ids));
			return DBCollection.Find(filter).SortBy(s => s.Id).ToList();
		}

		public List<ProjectSubTask> GetByTask(string projectTaskId)
		{
			var filter = Builders<ProjectSubTask>.Filter.In(s => s.ProjectTaskId, ValidIds(new[] { projectTaskId }));
			return DBCollection.Find(filter).SortBy(s => s.Id).ToList();
		}

		// On any of the projects, OR under any of the tasks. Both, because ProjectId
		// is missing on older sub-tasks - their task says which project they're on.
		public List<ProjectSubTask> GetInProjects(IEnumerable<string> projectIds, IEnumerable<string> projectTaskIds)
		{
			var filter = Builders<ProjectSubTask>.Filter.In(s => s.ProjectId, ValidIds(projectIds))
				| Builders<ProjectSubTask>.Filter.In(s => s.ProjectTaskId, ValidIds(projectTaskIds));
			return DBCollection.Find(filter).SortBy(s => s.Id).ToList();
		}

		public List<ProjectSubTask> GetTimeOff()
		{
			var filter = Builders<ProjectSubTask>.Filter.Ne(s => s.TimeOffType, null);
			var typeOnly = Builders<ProjectSubTask>.Projection.Include(s => s.TimeOffType);
			return DBCollection.Find(filter).Project<ProjectSubTask>(typeOnly).ToList();
		}
	}
}
