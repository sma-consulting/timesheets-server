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
		private ISecurityService _securityService;

		public TeamMemberService(ITeamMemberRepo repo, ISecurityService securityService)
		{
			_repo = repo;
			_securityService = securityService;
		}

		public TeamMember Create(TeamMember project)
		{
			_securityService.AuthorizeUser();
			return _repo.Create(project);
		}

		public TeamMember Get(string id)
		{
			_securityService.AuthorizeUser();
			return _repo.Get(id);
		}

		public List<TeamMember> GetAllTeamMembers()
		{
			_securityService.AuthorizeUser();
			return _repo.GetAll();
		}

		public TeamMember Update(TeamMember project)
		{
			_securityService.AuthorizeUser();
			return _repo.Update(project).Item2;
		}
	}
}
