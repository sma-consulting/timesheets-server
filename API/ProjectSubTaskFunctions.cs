using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class ProjectSubTaskFunctions : RequestHandler
	{

		private readonly IProjectSubTaskService _projectSubTaskService;
		private readonly ISecurityService _securityService;
		private readonly ITimeEntryRepo _timeEntryRepo;
		private readonly ILogger<ProjectSubTaskFunctions> _logger;

		public ProjectSubTaskFunctions(
			IProjectSubTaskService projectSubTaskService,
			ISecurityService securityService,
			ITimeEntryRepo timeEntryRepo,
			ILogger<ProjectSubTaskFunctions> logger)
		{
			_projectSubTaskService = projectSubTaskService;
			_securityService = securityService;
			_timeEntryRepo = timeEntryRepo;
			_logger = logger;
		}

		[Allow(Role.Admin)]
		[Function("CreateProjectSubTask")]
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


		// Admin-only. Refused while time is logged against the sub-task: those
		// entries feed payroll and invoices, and would be left pointing at a task
		// that no longer exists.
		[Allow(Role.Admin)]
		[Function("DeleteProjectSubTask")]
		public async Task<IActionResult> RunDeleteProjectSubTask(
			[HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "projectSubTask/{id}/")] HttpRequest req, string id)
		{
			if (!_securityService.IsCurrentUserAdmin())
			{
				return Forbidden();
			}

			int logged = _timeEntryRepo.GetAll().Count(t => t.ProjectSubTaskId == id);
			if (logged > 0)
			{
				return Invalid(string.Format(
					"This sub-task has {0} time entr{1} logged against it, so it can't be deleted.",
					logged, logged == 1 ? "y" : "ies"));
			}

			return Ok(
				() => _projectSubTaskService.Delete(id),
				(p) => new
				{
					deletedProjectSubTask = p
				});
		}


		[Allow(Role.SignedIn)]
		[Function("GetProjectSubTask")]
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


		[Allow(Role.Admin)]
		[Function("UpdateProjectSubTask")]
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


		[Allow(Role.SignedIn)]
		[Function("GetAllProjectSubTask")]
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
