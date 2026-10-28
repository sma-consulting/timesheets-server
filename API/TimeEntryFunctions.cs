using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class TimeEntryFunctions : RequestHandler
	{
		// Longest range one lock covers: past this the date filter narrows by whole
		// years only, and would lock days outside the range.
		public const int MaxLockMonths = 24;

		private readonly ITimeEntryService _timeEntryService;
		private readonly ILogger<TimeEntryFunctions> _logger;

		public TimeEntryFunctions(ITimeEntryService timeEntryService, ILogger<TimeEntryFunctions> logger)
		{
			_timeEntryService = timeEntryService;
			_logger = logger;
		}

		[Allow(Role.SignedIn)]
		[Function("CreateTimeEntry")]
		public async Task<IActionResult> RunCreateTimeEntry(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "timeEntry/")] HttpRequest req)
		{
			TimeEntry timeEntry = JsonConvert.DeserializeObject<TimeEntry>(
				await new StreamReader(req.Body).ReadToEndAsync());

			string invalid = _timeEntryService.Validate(timeEntry);
			if (invalid != null)
			{
				return Invalid(invalid);
			}

			if (!_timeEntryService.MayLogAgainst(
					_timeEntryService.OwnerFor(timeEntry), timeEntry.ProjectId))
			{
				return NotAssigned();
			}

			return Ok(
				() => _timeEntryService.Create(timeEntry),
				(t) => new
				{
					timeEntry = t
				});
		}


		[Allow(Role.SignedIn)]
		[Function("DeleteTimeEntry")]
		public async Task<IActionResult> RunDeleteTimeEntry(
			[HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "timeEntry/{id}/")] HttpRequest req, string id)
		{
			// Read once; the checks and the delete use it.
			TimeEntry existing = _timeEntryService.Find(id);
			if (existing == null)
			{
				return Invalid(Errors.TimeEntries.NotFound);
			}
			if (!_timeEntryService.MayWrite(existing))
			{
				return NotYours();
			}

			string locked = _timeEntryService.LockedReason(existing);
			if (locked != null)
			{
				return Invalid(locked);
			}

			return Ok(
				() => _timeEntryService.Delete(existing),
				(t) => new
				{
					deletedTimeEntry = t
				});
		}


		[Allow(Role.SignedIn)]
		[Function("GetTimeEntry")]
		public async Task<IActionResult> RunGetTimeEntry(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "timeEntry/{id}/")] HttpRequest req, string id)
		{
			TimeEntry existing = _timeEntryService.Find(id);
			if (existing == null)
			{
				return Invalid(Errors.TimeEntries.NotFound);
			}
			if (!_timeEntryService.MayRead(existing))
			{
				return NotYours();
			}

			return Ok(
				() => existing,
				(t) => new
				{
					timeEntry = t
				});
		}


		[Allow(Role.SignedIn)]
		[Function("UpdateTimeEntry")]
		public async Task<IActionResult> RunUpdateTimeEntry(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "timeEntry/{id}/update/")] HttpRequest req, string id)
		{
			TimeEntry timeEntry = JsonConvert.DeserializeObject<TimeEntry>(
				await new StreamReader(req.Body).ReadToEndAsync());
			timeEntry.Id = id;

			// Read once; the checks and the save use it.
			TimeEntry existing = _timeEntryService.Find(id);
			if (existing == null)
			{
				return Invalid(Errors.TimeEntries.NotFound);
			}

			// Checked before MayLogAgainst: reassigning someone else's entry to
			// yourself would otherwise pass, since OwnerFor rewrites the owner.
			if (!_timeEntryService.MayWrite(existing))
			{
				return NotYours();
			}

			string invalid = _timeEntryService.Validate(timeEntry);
			if (invalid != null)
			{
				return Invalid(invalid);
			}

			// After Validate, so the entry compared is the one that would be saved.
			string locked = _timeEntryService.LockedReason(existing, timeEntry);
			if (locked != null)
			{
				return Invalid(locked);
			}

			if (!_timeEntryService.MayLogAgainst(
					_timeEntryService.OwnerFor(timeEntry), timeEntry.ProjectId))
			{
				return NotAssigned();
			}

			return Ok(
				() => _timeEntryService.Update(timeEntry, existing),
				(res) => new
				{
					newTimeEntry = res
				});
		}


		// Locks, or unlocks, time by hand: { "from": "2026-09-01", "to":
		// "2026-09-30", "locked": true }, plus "teamMemberId" and/or "projectId"
		// to narrow it to one person or project (everyone's, on every project,
		// when left out). Locked time can't be changed until it's unlocked.
		[Allow(Role.Admin)]
		[Function("LockTimeEntries")]
		public async Task<IActionResult> RunLockTimeEntries(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "timeEntry/lock/")] HttpRequest req)
		{
			LockRequest request = JsonConvert.DeserializeObject<LockRequest>(
				await new StreamReader(req.Body).ReadToEndAsync());

			if (!DateOnly.TryParseExact(request?.From, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly from)
				|| !DateOnly.TryParseExact(request?.To, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly to))
			{
				return Invalid(Errors.TimeEntries.LockRange);
			}
			if (from > to)
			{
				return Invalid(Errors.Common.DateRangeReversed);
			}
			if ((to.Year - from.Year) * 12 + to.Month - from.Month + 1 > MaxLockMonths)
			{
				return Invalid(Errors.TimeEntries.LockTooLong);
			}

			string teamMemberId = string.IsNullOrWhiteSpace(request.TeamMemberId) ? null : request.TeamMemberId.Trim();
			string projectId = string.IsNullOrWhiteSpace(request.ProjectId) ? null : request.ProjectId.Trim();

			return Ok(
				() => _timeEntryService.Lock(teamMemberId, projectId, from, to, request.Locked),
				(count) => new
				{
					count
				});
		}


		private class LockRequest
		{
			public string From { get; set; }
			public string To { get; set; }
			public string TeamMemberId { get; set; }
			public string ProjectId { get; set; }
			public bool Locked { get; set; }
		}


		// timeEntry/?teamMemberId=&projectId=&from=&to= - at least one, so a page
		// gets only what it shows. Unfiltered, an admin would get every entry
		// there is, which grows by thousands a month.
		[Allow(Role.SignedIn)]
		[Function("GetAllTimeEntry")]
		public async Task<IActionResult> RunGetAllTimeEntry(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "timeEntry/")] HttpRequest req)
		{
			ApiError badDates = ReadDateRange(req, out DateOnly? from, out DateOnly? to);
			if (badDates != null)
			{
				return Invalid(badDates);
			}
			string teamMemberId = QueryValue(req, "teamMemberId");
			string projectId = QueryValue(req, "projectId");
			if (teamMemberId == null && projectId == null && from == null && to == null)
			{
				return Invalid(Errors.TimeEntries.FilterRequired);
			}

			return Ok(
				() => _timeEntryService.GetTimeEntries(teamMemberId, projectId, from, to),
				(t) => new
				{
					timeEntryList = t
				});
		}
	}
}
