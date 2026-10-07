using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class ProjectAssignmentService : IProjectAssignmentService
	{
		private IProjectAssignmentRepo _repo;
		private ISecurityService _securityService;

		public ProjectAssignmentService(IProjectAssignmentRepo repo, ISecurityService securityService)
		{
			_repo = repo;
			_securityService = securityService;
		}

		public ProjectAssignment Create(ProjectAssignment projectAssignment)
		{
			_securityService.AuthorizeUser();
			return _repo.Create(projectAssignment);
		}

		public ProjectAssignment Delete(string id)
		{
			_securityService.AuthorizeUser();
			return _repo.Delete(id);
		}

		public List<ProjectAssignment> GetByProject(string projectId)
		{
			_securityService.AuthorizeUser();
			return _repo.GetByProject(projectId);
		}

		public List<ProjectAssignment> GetByTeamMember(string teamMemberId)
		{
			_securityService.AuthorizeUser();
			return _repo.GetByTeamMember(teamMemberId);
		}
	}
}
