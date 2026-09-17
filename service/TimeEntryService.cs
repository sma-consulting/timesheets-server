using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class TimeEntryService : ITimeEntryService
	{
		private ITimeEntryRepo _repo;
		private ISecurityService _securityService;

		public TimeEntryService(ITimeEntryRepo repo, ISecurityService securityService)
		{
			_repo = repo;
			_securityService = securityService;
		}

		public TimeEntry Create(TimeEntry timeEntry)
		{
			_securityService.AuthorizeUser();
			return _repo.Create(timeEntry);
		}

		public TimeEntry Update(TimeEntry timeEntry)
		{
			_securityService.AuthorizeUser();
			return _repo.Update(timeEntry).Item2;
		}

		public TimeEntry Get(string id)
		{
			_securityService.AuthorizeUser();
			return _repo.Get(id);
		}

		public TimeEntry Delete(string id)
		{
			_securityService.AuthorizeUser();
			return _repo.Delete(id);
		}

		public List<TimeEntry> GetAllTimeEntries()
		{
			_securityService.AuthorizeUser();
			return _repo.GetAll();
		}
	}
}
