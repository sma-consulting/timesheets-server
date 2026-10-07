using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class TimeEntryFunctions : RequestHandler
	{

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
					_timeEntryService.OwnerFor(timeEntry), timeEntry.ProjectSubTaskId))
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
			if (!_timeEntryService.MayWrite(id))
			{
				return NotYours();
			}

			string locked = _timeEntryService.LockedReason(id);
			if (locked != null)
			{
				return Invalid(locked);
			}

			return Ok(
				() => _timeEntryService.Delete(id),
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
			if (!_timeEntryService.MayRead(id))
			{
				return NotYours();
			}

			return Ok(
				() => _timeEntryService.Get(id),
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

			// Checked before MayLogAgainst: reassigning someone else's entry to
			// yourself would otherwise pass, since OwnerFor rewrites the owner.
			if (!_timeEntryService.MayWrite(id))
			{
				return NotYours();
			}

			string invalid = _timeEntryService.Validate(timeEntry);
			if (invalid != null)
			{
				return Invalid(invalid);
			}

			// After Validate, so the entry compared is the one that would be saved.
			string locked = _timeEntryService.LockedReason(id, timeEntry);
			if (locked != null)
			{
				return Invalid(locked);
			}

			if (!_timeEntryService.MayLogAgainst(
					_timeEntryService.OwnerFor(timeEntry), timeEntry.ProjectSubTaskId))
			{
				return NotAssigned();
			}

			return Ok(
				() => _timeEntryService.Update(timeEntry),
				(res) => new
				{
					newTimeEntry = res
				});
		}


		[Allow(Role.SignedIn)]
		[Function("GetAllTimeEntry")]
		public async Task<IActionResult> RunGetAllTimeEntry(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "timeEntry/")] HttpRequest req)
		{
			return Ok(
				() => _timeEntryService.GetAllTimeEntries(),
				(t) => new
				{
					timeEntryList = t
				});
		}
	}
}
