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
        private readonly ILogger<ProjectConfigFunctions> _logger;

        public ProjectConfigFunctions(IProjectService projectService, ILogger<ProjectConfigFunctions> logger)
        {
            _projectService = projectService;
            _logger = logger;
        }

        [FunctionName("CreateProject")]
        public async Task<IActionResult> RunCreateProject(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "project/")] HttpRequest req)
        {
            Project project = JsonConvert.DeserializeObject<Project>(
                await new StreamReader(req.Body).ReadToEndAsync());

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
            Project project = JsonConvert.DeserializeObject<Project>(
                await new StreamReader(req.Body).ReadToEndAsync());
            project.Id = id;

            return Ok(
                () => _projectService.Update(project),
                (res) => new
                {
                    newProject = res
                });
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
