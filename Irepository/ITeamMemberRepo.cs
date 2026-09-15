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
	}
}
