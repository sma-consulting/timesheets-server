using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal interface IProjectTaskService
	{

		ProjectTask Create(ProjectTask projectTask);

		ProjectTask Update(ProjectTask projectTask);

		ProjectTask Get(string id);

		ProjectTask Delete(string id);

		List<ProjectTask> GetAllProjectTasks();
	}
}
