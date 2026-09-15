
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class ProjectService : IProjectService
	{

		private IProjectRepo _repo;
		private ISecurityService _securityService;

		public ProjectService(IProjectRepo repo, ISecurityService securityService)
		{
			_repo = repo;
			_securityService = securityService;
		}

		public Project Create(Project newProject)
		{
			_securityService.AuthorizeUser();
			return _repo.Create(newProject);
		}

		public Project Update(Project project)
		{
			_securityService.AuthorizeUser();
			return _repo.Update(project).Item2;
		}

		public Project Get(string id)
		{
			_securityService.AuthorizeUser();
			return _repo.Get(id);
		}

		public List<Project> GetAllProjects()
		{
			_securityService.AuthorizeUser();
			return _repo.GetAll();
		}
	}
}
