using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal interface IProjectAssignmentRepo
	{
		ProjectAssignment Create(ProjectAssignment data);
		ProjectAssignment Delete(string id);
		ProjectAssignment Get(string id);
		List<ProjectAssignment> GetAll();
		Tuple<ProjectAssignment, ProjectAssignment> Update(ProjectAssignment data);
		List<ProjectAssignment> GetByProject(string projectId);

		// Everyone's assignments on any of the projects.
		List<ProjectAssignment> GetByProjects(IEnumerable<string> projectIds);
		List<ProjectAssignment> GetByTeamMember(string teamMemberId);

		// How many assignments are in the role.
		long CountByRole(string projectRoleId);

		// Whether the team member is assigned to the project.
		bool IsAssigned(string teamMemberId, string projectId);
	}
}
