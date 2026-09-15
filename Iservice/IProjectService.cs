using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal interface IProjectService
	{

		Project Create(Project project);

		Project Update(Project project);

		Project Get(string id);

		List<Project> GetAllProjects();
	}
}
