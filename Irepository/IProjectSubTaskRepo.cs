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
	}
}
