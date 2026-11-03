using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class ProjectAssignmentFunctions : RequestHandler
	{
		private readonly IProjectAssignmentService _projectAssignmentService;

		public ProjectAssignmentFunctions(IProjectAssignmentService projectAssignmentService)
		{
			_projectAssignmentService = projectAssignmentService;
		}

		[Allow(Role.Admin)]
		[Function("CreateProjectAssignment")]
		public async Task<IActionResult> RunCreateProjectAssignment(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projectAssignment/")] HttpRequest req)
		{
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
