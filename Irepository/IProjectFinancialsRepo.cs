using System;
using System.Collections.Generic;

namespace sma.plan
{
	internal interface IProjectFinancialsRepo
	{
		List<ProjectFinancials> GetAll();

		// The project's financials, or null if it has none yet.
		ProjectFinancials GetByProject(string projectId);

		// The financials of any of the projects.
		List<ProjectFinancials> GetByProjects(IEnumerable<string> projectIds);

		// Creates the project's financials, or replaces them.
		ProjectFinancials Save(ProjectFinancials financials);

		void DeleteByProject(string projectId);

		// Copies a project's financials from its own document, where they were
		// kept before ProjectFinancials, unless it already has them here.
		void EnsureFromLegacy(string projectId);

		// The same for every project.
		void EnsureAllFromLegacy();
	}
}
