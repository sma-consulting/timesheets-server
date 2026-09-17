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
		private ISecurityService _securityService;

		public ProjectTaskService(IProjectTaskRepo repo, ISecurityService securityService)
		{
			_repo = repo;
			_securityService = securityService;
		}

		public ProjectTask Create(ProjectTask projectTask)
		{
			_securityService.AuthorizeUser();
			return _repo.Create(projectTask);
		}

		public ProjectTask Update(ProjectTask projectTask)
		{
			_securityService.AuthorizeUser();
			return _repo.Update(projectTask).Item2;
		}

		public ProjectTask Get(string id)
		{
			_securityService.AuthorizeUser();
			return _repo.Get(id);
		}

		public ProjectTask Delete(string id)
		{
			_securityService.AuthorizeUser();
			return _repo.Delete(id);
		}

		public List<ProjectTask> GetAllProjectTasks()
		{
			_securityService.AuthorizeUser();
			return _repo.GetAll();
		}
	}
}
