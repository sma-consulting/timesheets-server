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
		private ISecurityService _securityService;

		public ProjectSubTaskService(IProjectSubTaskRepo repo, ISecurityService securityService)
		{
			_repo = repo;
			_securityService = securityService;
		}

		public ProjectSubTask Create(ProjectSubTask projectSubTask)
		{
			_securityService.AuthorizeUser();
			return _repo.Create(projectSubTask);
		}

		public ProjectSubTask Update(ProjectSubTask projectSubTask)
		{
			_securityService.AuthorizeUser();
			return _repo.Update(projectSubTask).Item2;
		}

		public ProjectSubTask Get(string id)
		{
			_securityService.AuthorizeUser();
			return _repo.Get(id);
		}

		public ProjectSubTask Delete(string id)
		{
			_securityService.AuthorizeUser();
			return _repo.Delete(id);
		}

		public List<ProjectSubTask> GetAllProjectSubTasks()
		{
			_securityService.AuthorizeUser();
			return _repo.GetAll();
		}
	}
}
