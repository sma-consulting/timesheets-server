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
	}
}
