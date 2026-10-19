using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class TeamMemberService : ITeamMemberService
	{

		private ITeamMemberRepo _repo;

		public TeamMemberService(ITeamMemberRepo repo)
		{
			_repo = repo;
		}

		public TeamMember Create(TeamMember project)
		{
			return _repo.Create(project);
		}

		public TeamMember Get(string id)
		{
			return _repo.Get(id);
		}

		public List<TeamMember> GetAllTeamMembers()
		{
			return _repo.GetAll();
		}

		public TeamMember Update(TeamMember project)
		{
			return _repo.Update(project).Item2;
		}
	}
}
