using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using MongoDB.Driver.Core.Events;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class TimeCardFunctions : RequestHandler
	{

		private ITeamMemberService _teamMemberService;
		private ITimeCardService _timeCardService;
		private ILogger<TeamMemberConfigFunctions> _logger;

		public TimeCardFunctions(ITeamMemberService teamMemberService, ITimeCardService timeCardService, ILogger<TeamMemberConfigFunctions> logger)
		{
			_teamMemberService = teamMemberService;
			_logger = logger;
		}

		[FunctionName("CreateTimeCard")]
		public async Task<IActionResult> RunCreateTimeCard(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "teamMember/{id}/timeCards/{date}/create")] HttpRequest req,
			string id, string date)
		{
			
			if (DateOnly.TryParseExact(date, "yyyy-MM-dd", out DateOnly resultDate)) {
				return Ok(
				() => _timeCardService.CreateTimeCard(id, resultDate),
				(p) => new
				{
					timeCard = p
				});
			} else
			{
				return new BadRequestObjectResult(new { message = "Invalid Date Format (yyyy-MM-dd)." });
			}
			
		}


		[FunctionName("DeleteTeamMember")]
		public async Task<IActionResult> RunDeleteTeamMember(
			[HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "teamMember/{id}/delete/")] HttpRequest req, string id)
		{
			return Ok(
				() => (new DatabaseRepo<TeamMember>()).Delete(id),
				(p) => new
				{
					deletedTeamMember = p
				});
		}


		[FunctionName("GetTimeCard")]
		public async Task<IActionResult> RunGetTimeCard(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "teamMember/{id}/timeCard/{month}")] HttpRequest req, string id)
		{
			return Ok(
				() => _teamMemberService.Get(id),
				(p) => new
				{
					teamMember = p
				});
		}


		[FunctionName("UpdateTeamMember")]
		public async Task<IActionResult> RunUpdateTeamMember(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "teamMember/{id}/update/")] HttpRequest req, string id)
		{
			TeamMember teamMember = JsonConvert.DeserializeObject<TeamMember>(
				await new StreamReader(req.Body).ReadToEndAsync());
			teamMember.Id = id;

			return Ok(
				() => _teamMemberService.Update(teamMember),
				(res) => new
				{
					newTeamMember = res,
				});
		}


		[FunctionName("GetAllTeamMember")]
		public async Task<IActionResult> RunGetAllTeamMember(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "teamMember/")] HttpRequest req)
		{
			return Ok(
				() => _teamMemberService.GetAllTeamMembers(),
				(p) => new
				{
					teamMemberList = p
				});
		}
	}
}
