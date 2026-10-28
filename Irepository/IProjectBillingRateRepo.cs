using System;
using System.Collections.Generic;

namespace sma.plan
{
	internal interface IProjectBillingRateRepo
	{
		ProjectBillingRate Create(ProjectBillingRate data);
		ProjectBillingRate Delete(string id);
		ProjectBillingRate Get(string id);
		List<ProjectBillingRate> GetAll();
		Tuple<ProjectBillingRate, ProjectBillingRate> Update(ProjectBillingRate data);
		List<ProjectBillingRate> GetByProject(string projectId);

		// The rates on any of the projects.
		List<ProjectBillingRate> GetByProjects(IEnumerable<string> projectIds);

		// How many rates are for the role.
		long CountByRole(string projectRoleId);
	}
}
