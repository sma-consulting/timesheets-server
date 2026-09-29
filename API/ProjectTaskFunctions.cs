using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class ProjectTaskFunctions : RequestHandler
	{

		private readonly IProjectTaskService _projectTaskService;
		private readonly ISecurityService _securityService;
		private readonly ITimeEntryRepo _timeEntryRepo;
		private readonly IProjectSubTaskRepo _subTaskRepo;
		private readonly ILogger<ProjectTaskFunctions> _logger;

		public ProjectTaskFunctions(
			IProjectTaskService projectTaskService,
			ISecurityService securityService,
			ITimeEntryRepo timeEntryRepo,
			IProjectSubTaskRepo subTaskRepo,
			ILogger<ProjectTaskFunctions> logger)
		{
			_projectTaskService = projectTaskService;
			_securityService = securityService;
			_timeEntryRepo = timeEntryRepo;
			_subTaskRepo = subTaskRepo;
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


		// Admin-only. Deletes the task's sub-tasks with it - they can't exist
		// without it. Refused while time is logged against the task or any of its
		// sub-tasks, so no entry is ever left pointing at a deleted task.
		[FunctionName("DeleteProjectTask")]
		public async Task<IActionResult> RunDeleteProjectTask(
			[HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "projectTask/{id}/")] HttpRequest req, string id)
		{
			if (!_securityService.IsCurrentUserAdmin())
			{
				return Forbidden();
			}

			var subTaskIds = _subTaskRepo.GetAll()
				.Where(s => s.ProjectTaskId == id)
				.Select(s => s.Id)
				.ToList();

			int logged = _timeEntryRepo.GetAll().Count(t =>
				t.ProjectTaskId == id ||
				(t.ProjectSubTaskId != null && subTaskIds.Contains(t.ProjectSubTaskId)));

			if (logged > 0)
			{
				return Invalid(string.Format(
					"This task has {0} time entr{1} logged against it or its sub-tasks, so it can't be deleted.",
					logged, logged == 1 ? "y" : "ies"));
			}

			return Ok(
				() =>
				{
					foreach (var subTaskId in subTaskIds)
					{
						_subTaskRepo.Delete(subTaskId);
					}
					return _projectTaskService.Delete(id);
				},
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
