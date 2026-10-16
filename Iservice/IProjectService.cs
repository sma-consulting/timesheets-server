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

		// Null when the project can be saved, otherwise the reason it can't.
		// Also tidies it: trims text, and sets Billable from the project type.
		string Validate(Project project);

		// Saves a new project and puts its project manager on its team.
		Project CreateWithManager(Project project);

		// Puts the project's manager on its team, if they aren't on it yet.
		void AddManagerToTeam(Project project);

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
