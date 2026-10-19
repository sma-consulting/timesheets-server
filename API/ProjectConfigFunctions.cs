using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
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
        private readonly IProjectFinancialsRepo _financialsRepo;
        private readonly ILogger<ProjectConfigFunctions> _logger;

        public ProjectConfigFunctions(
            IProjectService projectService,
            IProjectFinancialsRepo financialsRepo,
            ILogger<ProjectConfigFunctions> logger)
        {
            _projectService = projectService;
            _financialsRepo = financialsRepo;
            _logger = logger;
        }

        [Allow(Role.Admin)]
        [Function("CreateProject")]
        public async Task<IActionResult> RunCreateProject(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "project/")] HttpRequest req)
        {
            Project project = JsonConvert.DeserializeObject<Project>(
                await new StreamReader(req.Body).ReadToEndAsync());

            string invalid = _projectService.Validate(project);
            if (invalid != null)
            {
                return Invalid(invalid);
            }

            return Ok(
                () => _projectService.CreateWithManager(project),
                (p) => new
                {
                    project = p
                });
        }


        [Allow(Role.Admin)]
        [Function("DeleteProject")]
        public async Task<IActionResult> RunDeleteProject(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "project/{id}/")] HttpRequest req, string id)
        {
            return Ok(
                () =>
                {
                    Project deleted = (new DatabaseRepo<Project>()).Delete(id);
                    // Its financials go with it.
                    _financialsRepo.DeleteByProject(id);
                    return deleted;
                },
                (p) => new
                {
                    deletedProject = p
                });
        }


        [Allow(Role.SignedIn)]
        [Function("GetProject")]
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


        [Allow(Role.Admin)]
        [Function("UpdateProject")]
        public async Task<IActionResult> RunUpdateProject(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "project/{id}/update/")] HttpRequest req, string id)
        {
            Project project = JsonConvert.DeserializeObject<Project>(
                await new StreamReader(req.Body).ReadToEndAsync());
            project.Id = id;

            string invalid = _projectService.Validate(project);
            if (invalid != null)
            {
                return Invalid(invalid);
            }

            return Ok(
                () =>
                {
                    // Saving replaces the whole project document, which would drop
                    // financials still kept on it from before they had their own -
                    // so they're copied across first.
                    _financialsRepo.EnsureFromLegacy(id);
                    Project updated = _projectService.Update(project);
                    _projectService.AddManagerToTeam(project);
                    return updated;
                },
                (res) => new
                {
                    newProject = res
                });
        }


        [Allow(Role.SignedIn)]
        [Function("GetAllProject")]
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
