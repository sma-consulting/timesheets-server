using System;
using System.Collections.Generic;

namespace sma.plan
{
	internal interface IProjectRoleRepo
	{
		ProjectRole Create(ProjectRole data);
		ProjectRole Delete(string id);
		ProjectRole Get(string id);
		List<ProjectRole> GetAll();
		Tuple<ProjectRole, ProjectRole> Update(ProjectRole data);

		// Whether a role other than `exceptId` has this name, ignoring case and
		// spaces at either end.
		bool NameTaken(string name, string exceptId);

		// The Project Manager role - by its code (PM) or its name - or null if
		// there isn't one.
		ProjectRole GetProjectManager();
	}
}
