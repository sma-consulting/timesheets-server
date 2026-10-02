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
	internal class ProjectAssignmentFunctions : RequestHandler
	{
		private readonly IProjectAssignmentService _projectAssignmentService;
		private readonly ISecurityService _securityService;
		private readonly ILogger<ProjectAssignmentFunctions> _logger;

		public ProjectAssignmentFunctions(
			IProjectAssignmentService projectAssignmentService,
			ISecurityService securityService,
			ILogger<ProjectAssignmentFunctions> logger)
		{
			_projectAssignmentService = projectAssignmentService;
			_securityService = securityService;
			_logger = logger;
		}

		[Allow(Role.Admin)]
		[Function("CreateProjectAssignment")]
		public async Task<IActionResult> RunCreateProjectAssignment(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projectAssignment/")] HttpRequest req)
		{
			if (!_securityService.IsCurrentUserAdmin())
			{
				return Forbidden();
			}

			ProjectAssignment assignment = JsonConvert.DeserializeObject<ProjectAssignment>(
				await new StreamReader(req.Body).ReadToEndAsync());

			return Ok(
				() => _projectAssignmentService.Create(assignment),
				(p) => new
				{
					projectAssignment = p
				});
		}


		[Allow(Role.Admin)]
		[Function("DeleteProjectAssignment")]
		public async Task<IActionResult> RunDeleteProjectAssignment(
			[HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "projectAssignment/{id}/")] HttpRequest req, string id)
		{
			if (!_securityService.IsCurrentUserAdmin())
			{
				return Forbidden();
			}

			return Ok(
				() => _projectAssignmentService.Delete(id),
				(p) => new
				{
					deletedProjectAssignment = p
				});
		}


		[Allow(Role.SignedIn)]
		[Function("GetProjectAssignments")]
		public async Task<IActionResult> RunGetProjectAssignments(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projectAssignment/")] HttpRequest req)
		{
			string projectId = req.Query["projectId"];
			string teamMemberId = req.Query["teamMemberId"];

			return Ok(
				() => string.IsNullOrWhiteSpace(projectId)
					? _projectAssignmentService.GetByTeamMember(teamMemberId)
					: _projectAssignmentService.GetByProject(projectId),
				(p) => new
				{
					projectAssignmentList = p
				});
		}
	}
}
