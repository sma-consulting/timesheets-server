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
		Project Find(string id);
		List<Project> GetAll();
		Tuple<Project, Project> Update(Project data);

		// Whether a project with the id exists.
		bool Exists(string id);

		// The projects with these ids.
		List<Project> GetByIds(IEnumerable<string> ids);

		// The same projects with only their id and rate list read.
		List<Project> GetRateListsOf(IEnumerable<string> ids);

		// The ids of the projects the team member is Project Manager of.
		HashSet<string> IdsManagedBy(string teamMemberId);

		// How many projects belong to the customer.
		long CountByCustomer(string customerId);

		// The ids of the paid hours off projects (Project.PaidTimeOff) - one, in
		// practice.
		HashSet<string> IdsPaidTimeOff();

		// The projects that use a rate list: only their name, code and list.
		List<Project> GetRateListUse();

		// Points the project at a rate list (null for none), in one update.
		// False when there's no such project.
		bool SetRateList(string projectId, string rateListId);

		// How many projects use the rate list.
		long CountByRateList(string rateListId);
	}
}
