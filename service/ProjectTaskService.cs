using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class ProjectTaskService : IProjectTaskService
	{
		private IProjectTaskRepo _repo;
		private IProjectService _projectService;

		public ProjectTaskService(
			IProjectTaskRepo repo,
			IProjectService projectService)
		{
			_repo = repo;
			_projectService = projectService;
		}

		public ProjectTask Create(ProjectTask projectTask)
		{
			return _repo.Create(projectTask);
		}

		public ProjectTask Update(ProjectTask projectTask)
		{
			return _repo.Update(projectTask).Item2;
		}

		public ProjectTask Get(string id)
		{
			return _repo.Get(id);
		}

		public ProjectTask Delete(string id)
		{
			return _repo.Delete(id);
		}

		// A task inherits its project's visibility. Filtering here as well as on
		// the project list stops the work breakdown leaking the names of projects
		// the caller was never shown.
		public List<ProjectTask> GetAllProjectTasks()
		{
			HashSet<string> visible = _projectService.VisibleProjectIds();

			return visible == null
				? _repo.GetAll()
				: _repo.GetByProjects(visible);
		}
	}
}
