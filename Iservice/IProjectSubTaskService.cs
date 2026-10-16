using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal interface IProjectSubTaskService
	{

		ProjectSubTask Create(ProjectSubTask projectSubTask);

		ProjectSubTask Update(ProjectSubTask projectSubTask);

		ProjectSubTask Get(string id);

		ProjectSubTask Delete(string id);

		List<ProjectSubTask> GetAllProjectSubTasks();
	}
}
