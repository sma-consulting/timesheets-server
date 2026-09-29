using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using MongoDB.Driver.Core.Events;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class TeamMemberConfigFunctions : RequestHandler
	{

        private ITeamMemberService _teamMemberService;
        private ILogger<TeamMemberConfigFunctions> _logger;


		public TeamMemberConfigFunctions(ITeamMemberService teamMemberService, ILogger<TeamMemberConfigFunctions> logger)
		{
			_teamMemberService = teamMemberService;
            _logger = logger;
		}  

		[FunctionName("CreateTeamMember")]
		public async Task<IActionResult> RunCreateTeamMember(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "teamMember/")] HttpRequest req)
		{
			TeamMember teamMember = JsonConvert.DeserializeObject<TeamMember>(
				await new StreamReader(req.Body).ReadToEndAsync());

            return Ok(
                () => _teamMemberService.Create(teamMember),
                (p) => new
                {
                    teamMember = p
                });
        }


		[FunctionName("DeleteTeamMember")]
        public async Task<IActionResult> RunDeleteTeamMember(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "teamMember/{id}/")] HttpRequest req, string id)
        {
            return Ok(
                () => (new DatabaseRepo<TeamMember>()).Delete(id),
                (p) => new
                {
                    deletedTeamMember = p
                });
        }


        [FunctionName("GetTeamMember")]
        public async Task<IActionResult> RunGetTeamMember(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "teamMember/{id}/")] HttpRequest req, string id)
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
