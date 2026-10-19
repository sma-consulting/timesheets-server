using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal interface IProjectSubTaskRepo
	{
		ProjectSubTask Create(ProjectSubTask data);
		ProjectSubTask Delete(string id);
		ProjectSubTask Get(string id);
		List<ProjectSubTask> GetAll();
		Tuple<ProjectSubTask, ProjectSubTask> Update(ProjectSubTask data);

		// Whether a sub-task with the id exists.
		bool Exists(string id);

		// The sub-tasks with these ids.
		List<ProjectSubTask> GetByIds(IEnumerable<string> ids);

		// The sub-tasks under a task.
		List<ProjectSubTask> GetByTask(string projectTaskId);

		// The sub-tasks on any of the projects, or under any of the tasks.
		List<ProjectSubTask> GetInProjects(IEnumerable<string> projectIds, IEnumerable<string> projectTaskIds);
	}
}
