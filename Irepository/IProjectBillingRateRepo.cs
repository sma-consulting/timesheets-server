using System;
using System.Collections.Generic;

namespace sma.plan
{
	internal interface IProjectBillingRateRepo
	{
		ProjectBillingRate Create(ProjectBillingRate data);
		ProjectBillingRate Delete(string id);
		ProjectBillingRate Get(string id);
		Tuple<ProjectBillingRate, ProjectBillingRate> Update(ProjectBillingRate data);

		// A rate list's rows; with `teamMemberIds`, only those people's.
		List<ProjectBillingRate> GetByList(string rateListId, IEnumerable<string> teamMemberIds = null);

		// Several lists' rows, in one read.
		List<ProjectBillingRate> GetByLists(IEnumerable<string> rateListIds);

		// What each project's hours are priced by: its list's rows, each tagged
		// with the project - a list two of them share comes once for each. A
		// project with no list has none.
		List<ProjectBillingRate> GetForProjects(IEnumerable<Project> projects);

		// Adds the rows in one go.
		void CreateMany(List<ProjectBillingRate> rows);

		// Ends those of `ids` still open-ended on `endDate`.
		long Close(IEnumerable<string> ids, DateOnly endDate);

		// Removes all of a list's rows.
		long DeleteByList(string rateListId);
	}
}
