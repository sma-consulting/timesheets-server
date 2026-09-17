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
	internal class ProjectTaskFunctions : RequestHandler
	{

		private readonly IProjectTaskService _projectTaskService;
		private readonly ILogger<ProjectTaskFunctions> _logger;

		public ProjectTaskFunctions(IProjectTaskService projectTaskService, ILogger<ProjectTaskFunctions> logger)
		{
			_projectTaskService = projectTaskService;
			_logger = logger;
		}

		[FunctionName("CreateProjectTask")]
		public async Task<IActionResult> RunCreateProjectTask(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projectTask/")] HttpRequest req)
		{
			ProjectTask projectTask = JsonConvert.DeserializeObject<ProjectTask>(
				await new StreamReader(req.Body).ReadToEndAsync());

			return Ok(
				() => _projectTaskService.Create(projectTask),
				(p) => new
				{
					projectTask = p
				});
		}


		[FunctionName("DeleteProjectTask")]
		public async Task<IActionResult> RunDeleteProjectTask(
			[HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "projectTask/{id}/delete/")] HttpRequest req, string id)
		{
			return Ok(
				() => _projectTaskService.Delete(id),
				(p) => new
				{
					deletedProjectTask = p
				});
		}


		[FunctionName("GetProjectTask")]
		public async Task<IActionResult> RunGetProjectTask(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projectTask/{id}/")] HttpRequest req, string id)
		{
			return Ok(
				() => _projectTaskService.Get(id),
				(p) => new
				{
					projectTask = p
				});
		}


		[FunctionName("UpdateProjectTask")]
		public async Task<IActionResult> RunUpdateProjectTask(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projectTask/{id}/update/")] HttpRequest req, string id)
		{
			ProjectTask projectTask = JsonConvert.DeserializeObject<ProjectTask>(
				await new StreamReader(req.Body).ReadToEndAsync());
			projectTask.Id = id;

			return Ok(
				() => _projectTaskService.Update(projectTask),
				(res) => new
				{
					newProjectTask = res
				});
		}


		[FunctionName("GetAllProjectTask")]
		public async Task<IActionResult> RunGetAllProjectTask(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projectTask/")] HttpRequest req)
		{
			return Ok(
				() => _projectTaskService.GetAllProjectTasks(),
				(p) => new
				{
					projectTaskList = p
				});
		}
	}
}
