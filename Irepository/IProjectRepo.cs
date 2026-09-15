using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal interface IProjectRepo
	{
		Project Create(Project data);
		Project Delete(string id);
		Project Get(string id);
		List<Project> GetAll();
		Tuple<Project, Project> Update(Project data);
	}
}
