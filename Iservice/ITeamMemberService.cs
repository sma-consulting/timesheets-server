using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal interface ITeamMemberService
	{
		TeamMember Create(TeamMember project);

		TeamMember Update(TeamMember project);

		TeamMember Get(string id);

		List<TeamMember> GetAllTeamMembers();
	}
}
