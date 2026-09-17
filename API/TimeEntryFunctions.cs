using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
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

		[FunctionName("CreateTimeEntry")]
		public async Task<IActionResult> RunCreateTimeEntry(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "timeEntry/")] HttpRequest req)
		{
			TimeEntry timeEntry = JsonConvert.DeserializeObject<TimeEntry>(
				await new StreamReader(req.Body).ReadToEndAsync());

			return Ok(
				() => _timeEntryService.Create(timeEntry),
				(t) => new
				{
					timeEntry = t
				});
		}


		[FunctionName("DeleteTimeEntry")]
		public async Task<IActionResult> RunDeleteTimeEntry(
			[HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "timeEntry/{id}/delete/")] HttpRequest req, string id)
		{
			return Ok(
				() => _timeEntryService.Delete(id),
				(t) => new
				{
					deletedTimeEntry = t
				});
		}


		[FunctionName("GetTimeEntry")]
		public async Task<IActionResult> RunGetTimeEntry(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "timeEntry/{id}/")] HttpRequest req, string id)
		{
			return Ok(
				() => _timeEntryService.Get(id),
				(t) => new
				{
					timeEntry = t
				});
		}


		[FunctionName("UpdateTimeEntry")]
		public async Task<IActionResult> RunUpdateTimeEntry(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "timeEntry/{id}/update/")] HttpRequest req, string id)
		{
			TimeEntry timeEntry = JsonConvert.DeserializeObject<TimeEntry>(
				await new StreamReader(req.Body).ReadToEndAsync());
			timeEntry.Id = id;

			return Ok(
				() => _timeEntryService.Update(timeEntry),
				(res) => new
				{
					newTimeEntry = res
				});
		}


		[FunctionName("GetAllTimeEntry")]
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
