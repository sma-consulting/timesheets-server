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

		public ProjectAssignmentService(IProjectAssignmentRepo repo)
		{
			_repo = repo;
		}

		public ProjectAssignment Create(ProjectAssignment projectAssignment)
		{
			return _repo.Create(projectAssignment);
		}

		public ProjectAssignment Delete(string id)
		{
			return _repo.Delete(id);
		}

		public List<ProjectAssignment> GetByProject(string projectId)
		{
			return _repo.GetByProject(projectId);
		}

		public List<ProjectAssignment> GetByTeamMember(string teamMemberId)
		{
			return _repo.GetByTeamMember(teamMemberId);
		}
	}
}
