using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal interface IProjectAssignmentService
	{
		ProjectAssignment Create(ProjectAssignment projectAssignment);
		ProjectAssignment Delete(string id);
		List<ProjectAssignment> GetByProject(string projectId);
		List<ProjectAssignment> GetByTeamMember(string teamMemberId);
	}
}
