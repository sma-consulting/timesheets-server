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
		List<ProjectAssignment> GetByTeamMember(string teamMemberId);
	}
}
