using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal interface IProjectTaskRepo
	{
		ProjectTask Create(ProjectTask data);
		ProjectTask Delete(string id);
		ProjectTask Get(string id);
		List<ProjectTask> GetAll();
		Tuple<ProjectTask, ProjectTask> Update(ProjectTask data);
	}
}
