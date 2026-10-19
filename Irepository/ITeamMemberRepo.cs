using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal interface ITeamMemberRepo
	{
		TeamMember Create(TeamMember data);
		TeamMember Delete(string id);
		TeamMember Get(string id);
		List<TeamMember> GetAll();
		Tuple<TeamMember, TeamMember> Update(TeamMember data);
		TeamMember GetByUserId(string userId);

		// Whether a team member with the id exists.
		bool Exists(string id);

		// The team members with these ids.
		List<TeamMember> GetByIds(IEnumerable<string> ids);

		// How many team members have the role among their AssignedRoles.
		long CountWithRole(string projectRoleId);
	}
}
