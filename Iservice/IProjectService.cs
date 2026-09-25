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

		bool MayView(string projectId);

		// Null means every project is visible.
		HashSet<string> VisibleProjectIds();

		// Projects the caller is Project Manager of.
		HashSet<string> ManagedProjectIds();
	}
}
