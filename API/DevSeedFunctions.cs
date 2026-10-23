using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace sma.plan
{
	// Loads test data for load testing - see DevSeed. Only where DevSeed is
	// enabled: ENABLE_DEV_SEED on and a database named ...LOADTEST, so it can
	// never touch real data. Open to anyone signed in there: a fresh test
	// database has no admins yet, and running it makes the caller one.
	internal class DevSeedFunctions : RequestHandler
	{
		private readonly ISecurityService _securityService;
		private readonly ITeamMemberRepo _teamMemberRepo;
		private readonly IProjectRepo _projectRepo;
		private readonly IProjectTaskRepo _taskRepo;
		private readonly IProjectSubTaskRepo _subTaskRepo;
		private readonly IHolidayRepo _holidayRepo;

		public DevSeedFunctions(
			ISecurityService securityService,
			ITeamMemberRepo teamMemberRepo,
			IProjectRepo projectRepo,
			IProjectTaskRepo taskRepo,
			IProjectSubTaskRepo subTaskRepo,
			IHolidayRepo holidayRepo)
		{
			_securityService = securityService;
			_teamMemberRepo = teamMemberRepo;
			_projectRepo = projectRepo;
			_taskRepo = taskRepo;
			_subTaskRepo = subTaskRepo;
			_holidayRepo = holidayRepo;
		}

		// Empties the test database and fills it with ?people= people (50 if
		// left out) over ?years= years (2) - the same data every time for the
		// same numbers. The caller becomes an admin and project manager in it.
		[Allow(Role.SignedIn)]
		[Function("SeedTestData")]
		public async Task<IActionResult> RunSeedTestData(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "dev/seed/")] HttpRequest req)
		{
			if (!DevSeed.Enabled())
			{
				return Forbidden(Errors.Dev.SeedOff);
			}

			int people = int.TryParse(QueryValue(req, "people"), out int p) ? p : 50;
			int years = int.TryParse(QueryValue(req, "years"), out int y) ? y : 2;
			if (people < 1 || people > DevSeed.MaxPeople || years < 1 || years > DevSeed.MaxYears)
			{
				return Invalid(Errors.Dev.SeedOptions);
			}

			// Read before the wipe, which takes the caller's records with it.
			string email = _securityService.GetCurrentEmail();
			string name = _teamMemberRepo.Get(_securityService.GetCurrentTeamMemberId())?.Name ?? email;

			return Ok(
				() =>
				{
					DevSeed.Wipe();

					// The paid hours off project, as at startup, for holidays,
					// leave and lunch breaks.
					PaidTimeOffSeed.Run(_projectRepo, _taskRepo, _subTaskRepo);
					List<string> paidTimeOff = _projectRepo.IdsPaidTimeOff().ToList();
					var timeOff = _subTaskRepo
						.GetInProjects(paidTimeOff, _taskRepo.GetByProjects(paidTimeOff).Select(t => t.Id))
						.Where(s => s.TimeOffType != null)
						.GroupBy(s => s.TimeOffType)
						.ToDictionary(g => g.Key, g => g.First());

					return new DevSeedGenerator(people, years, email, name, timeOff,
						_holidayRepo.GetAll().Select(h => h.Date)).Run();
				},
				(summary) => new
				{
					summary
				});
		}
	}
}
