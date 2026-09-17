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
	internal class ProjectSubTaskFunctions : RequestHandler
	{

		private readonly IProjectSubTaskService _projectSubTaskService;
		private readonly ILogger<ProjectSubTaskFunctions> _logger;

		public ProjectSubTaskFunctions(IProjectSubTaskService projectSubTaskService, ILogger<ProjectSubTaskFunctions> logger)
		{
			_projectSubTaskService = projectSubTaskService;
			_logger = logger;
		}

		[FunctionName("CreateProjectSubTask")]
		public async Task<IActionResult> RunCreateProjectSubTask(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projectSubTask/")] HttpRequest req)
		{
			ProjectSubTask projectSubTask = JsonConvert.DeserializeObject<ProjectSubTask>(
				await new StreamReader(req.Body).ReadToEndAsync());

			return Ok(
				() => _projectSubTaskService.Create(projectSubTask),
				(p) => new
				{
					projectSubTask = p
				});
		}


		[FunctionName("DeleteProjectSubTask")]
		public async Task<IActionResult> RunDeleteProjectSubTask(
			[HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "projectSubTask/{id}/delete/")] HttpRequest req, string id)
		{
			return Ok(
				() => _projectSubTaskService.Delete(id),
				(p) => new
				{
					deletedProjectSubTask = p
				});
		}


		[FunctionName("GetProjectSubTask")]
		public async Task<IActionResult> RunGetProjectSubTask(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projectSubTask/{id}/")] HttpRequest req, string id)
		{
			return Ok(
				() => _projectSubTaskService.Get(id),
				(p) => new
				{
					projectSubTask = p
				});
		}


		[FunctionName("UpdateProjectSubTask")]
		public async Task<IActionResult> RunUpdateProjectSubTask(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projectSubTask/{id}/update/")] HttpRequest req, string id)
		{
			ProjectSubTask projectSubTask = JsonConvert.DeserializeObject<ProjectSubTask>(
				await new StreamReader(req.Body).ReadToEndAsync());
			projectSubTask.Id = id;

			return Ok(
				() => _projectSubTaskService.Update(projectSubTask),
				(res) => new
				{
					newProjectSubTask = res
				});
		}


		[FunctionName("GetAllProjectSubTask")]
		public async Task<IActionResult> RunGetAllProjectSubTask(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projectSubTask/")] HttpRequest req)
		{
			return Ok(
				() => _projectSubTaskService.GetAllProjectSubTasks(),
				(p) => new
				{
					projectSubTaskList = p
				});
		}
	}
}
