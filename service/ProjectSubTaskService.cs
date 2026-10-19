using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class ProjectSubTaskService : IProjectSubTaskService
	{
		private IProjectSubTaskRepo _repo;
		private IProjectService _projectService;
		private IProjectTaskRepo _taskRepo;

		public ProjectSubTaskService(
			IProjectSubTaskRepo repo,
			IProjectService projectService,
			IProjectTaskRepo taskRepo)
		{
			_repo = repo;
			_projectService = projectService;
			_taskRepo = taskRepo;
		}

		public ProjectSubTask Create(ProjectSubTask projectSubTask)
		{
			return _repo.Create(projectSubTask);
		}

		public ProjectSubTask Update(ProjectSubTask projectSubTask)
		{
			return _repo.Update(projectSubTask).Item2;
		}

		public ProjectSubTask Get(string id)
		{
			return _repo.Get(id);
		}

		public ProjectSubTask Delete(string id)
		{
			return _repo.Delete(id);
		}

		// Visible at project level, same as ProjectTask.
		//
		// Reached through the parent task rather than ProjectSubTask.ProjectId.
		// That field is a denormalised convenience the task panel fills in, and
		// sub-tasks seeded before the panel existed do not have it - filtering on
		// it drops them all and empties the time log. ProjectTaskId is the link
		// the tree is actually built from, so it is the one to trust.
		public List<ProjectSubTask> GetAllProjectSubTasks()
		{
			HashSet<string> visible = _projectService.VisibleProjectIds();

			if (visible == null)
			{
				return _repo.GetAll();
			}

			List<string> visibleTaskIds = _taskRepo
				.GetByProjects(visible)
				.Select(t => t.Id)
				.ToList();

			return _repo.GetInProjects(visible, visibleTaskIds);
		}
	}
}
