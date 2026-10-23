using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace sma.plan
{
	// The Staff hours page, for admins: each staff member's hours over a span
	// of days, added up per day, project and kind of time off. Only that span's
	// entries are read, so the answer stays the same size however much time has
	// been logged overall.
	internal class StaffHoursFunctions : RequestHandler
	{
		// A month's page shows the whole weeks around it, so a little over a month.
		public const int MaxDays = 62;

		private readonly ITimeEntryRepo _timeEntryRepo;
		private readonly ITeamMemberRepo _teamMemberRepo;
		private readonly IProjectSubTaskRepo _subTaskRepo;

		public StaffHoursFunctions(
			ITimeEntryRepo timeEntryRepo,
			ITeamMemberRepo teamMemberRepo,
			IProjectSubTaskRepo subTaskRepo)
		{
			_timeEntryRepo = timeEntryRepo;
			_teamMemberRepo = teamMemberRepo;
			_subTaskRepo = subTaskRepo;
		}

		// staffHours/?from=2026-08-31&to=2026-10-04
		[Allow(Role.Admin)]
		[Function("GetStaffHours")]
		public async Task<IActionResult> RunGetStaffHours(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "staffHours/")] HttpRequest req)
		{
			ApiError badDates = ReadDateRange(req, out DateOnly? from, out DateOnly? to);
			if (badDates != null)
			{
				return Invalid(badDates);
			}
			if (from == null || to == null)
			{
				return Invalid(Errors.StaffHours.RangeRequired);
			}
			if (to.Value.DayNumber - from.Value.DayNumber + 1 > MaxDays)
			{
				return Invalid(Errors.StaffHours.RangeTooLong);
			}

			return Ok(
				() => HoursBetween(from.Value, to.Value),
				(s) => new
				{
					staff = s
				});
		}

		// Staff are the team members with a sign-in; planning placeholders have
		// none. Everyone's listed, with or without hours.
		private object HoursBetween(DateOnly from, DateOnly to)
		{
			// Which kind of time off each paid-hours-off sub-task is - vacation, a
			// stat holiday... - whatever it's called.
			Dictionary<string, string> timeOffTypes = _subTaskRepo.GetAll()
				.Where(s => s.TimeOffType != null)
				.ToDictionary(s => s.Id, s => s.TimeOffType);

			ILookup<string, TimeEntry> byPerson = _timeEntryRepo.GetHoursBetween(from, to)
				.Where(e => e.TeamMemberId != null)
				.ToLookup(e => e.TeamMemberId);

			return _teamMemberRepo.GetAll()
				.Where(m => !string.IsNullOrWhiteSpace(m.UserId))
				.Select(m => new
				{
					id = m.Id,
					name = m.Name,
					entries = byPerson[m.Id]
						.GroupBy(e => (
							e.Date,
							e.ProjectId,
							TimeOffType: e.ProjectSubTaskId != null && timeOffTypes.TryGetValue(e.ProjectSubTaskId, out string type) ? type : null))
						.Select(day => new
						{
							date = day.Key.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
							projectId = day.Key.ProjectId,
							hours = day.Sum(e => e.Hours),
							timeOffType = day.Key.TimeOffType,
						})
						.ToList(),
				})
				.ToList();
		}
	}
}
