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
	internal class ProjectConfigFunctions : RequestHandler
	{

        private readonly IProjectService _projectService;
        private readonly ISecurityService _securityService;
        private readonly ILogger<ProjectConfigFunctions> _logger;

        public ProjectConfigFunctions(IProjectService projectService, ISecurityService securityService, ILogger<ProjectConfigFunctions> logger)
        {
            _projectService = projectService;
            _securityService = securityService;
            _logger = logger;
        }

        [FunctionName("CreateProject")]
        public async Task<IActionResult> RunCreateProject(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "project/")] HttpRequest req)
        {
            if (!_securityService.IsCurrentUserAdmin())
            {
                return Forbidden();
            }

            Project project = JsonConvert.DeserializeObject<Project>(
                await new StreamReader(req.Body).ReadToEndAsync());

            string invalid = Validate(project);
            if (invalid != null)
            {
                return Invalid(invalid);
            }

            return Ok(
                () => _projectService.Create(project),
                (p) => new
                {
                    project = p
                });
        }


        [FunctionName("DeleteProject")]
        public async Task<IActionResult> RunDeleteProject(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "project/{id}/delete/")] HttpRequest req, string id)
        {
            if (!_securityService.IsCurrentUserAdmin())
            {
                return Forbidden();
            }

            return Ok(
                () => (new DatabaseRepo<Project>()).Delete(id),
                (p) => new
                {
                    deletedProject = p
                });
        }


        [FunctionName("GetProject")]
        public async Task<IActionResult> RunGetProject(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "project/{id}/")] HttpRequest req, string id)
        {
            if (!_projectService.MayView(id))
            {
                return Forbidden("You are not assigned to that project.");
            }

            return Ok(
                () => _projectService.Get(id),
                (p) => new
                {
                    project = p
                });
        }


        [FunctionName("UpdateProject")]
        public async Task<IActionResult> RunUpdateProject(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "project/{id}/update/")] HttpRequest req, string id)
        {
            if (!_securityService.IsCurrentUserAdmin())
            {
                return Forbidden();
            }

            Project project = JsonConvert.DeserializeObject<Project>(
                await new StreamReader(req.Body).ReadToEndAsync());
            project.Id = id;

            string invalid = Validate(project);
            if (invalid != null)
            {
                return Invalid(invalid);
            }

            return Ok(
                () => _projectService.Update(project),
                (res) => new
                {
                    newProject = res
                });
        }


        // Returns null when the project is valid, otherwise the reason it isn't.
        // Also tidies what it checks: trims text, and derives Billable from the
        // project type so the two can't disagree.
        private static string Validate(Project project)
        {
            if (project == null)
            {
                return "No project was sent.";
            }

            project.ProjectType = string.IsNullOrWhiteSpace(project.ProjectType)
                ? Project.TypeHourly
                : project.ProjectType.Trim();

            if (!Project.ProjectTypes.Contains(project.ProjectType))
            {
                return "Project type must be Hourly, LumpSum or NonBillable.";
            }

            if (project.StartDate.HasValue && project.EndDate.HasValue &&
                project.EndDate.Value < project.StartDate.Value)
            {
                return "The end date can't be before the start date.";
            }

            project.Name = project.Name?.Trim();
            project.FundingCode = project.FundingCode?.Trim();
            project.Division = project.Division?.Trim();
            project.PoNumber = project.PoNumber?.Trim();
            project.Billable = project.ProjectType != Project.TypeNonBillable;
            return null;
        }


        [FunctionName("GetAllProject")]
        public async Task<IActionResult> RunGetAllProject(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "project/")] HttpRequest req)
        {
            return Ok(
                () => _projectService.GetAllProjects(),
                (p) => new
                {
                    projectList = p
                });
        }
    }
}
