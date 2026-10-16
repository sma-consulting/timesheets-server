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
    internal class ProjectTemplateConfigFunctions : RequestHandler
    {

        private ILogger<ProjectTemplateConfigFunctions> _logger;


		public ProjectTemplateConfigFunctions(ILogger<ProjectTemplateConfigFunctions> logger)
        {
            _logger = logger;
        }

        [Allow(Role.Admin)]
        [Function("CreateProjectTemplate")]
        public static async Task<IActionResult> RunCreateProjectTemplate(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projectTemplate/create/")] HttpRequest req)
        {
            ProjectTemplate projectTemplate = JsonConvert.DeserializeObject<ProjectTemplate>(
                await new StreamReader(req.Body).ReadToEndAsync());

            return Ok(
                () => (new DatabaseRepo<ProjectTemplate>()).Create(projectTemplate),
                (p) => new
                {
                    projectTemplate = p
                });
        }


        [Allow(Role.Admin)]
        [Function("DeleteProjectTemplate")]
        public static async Task<IActionResult> RunDeleteProjectTemplate(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "projectTemplate/{id}/")] HttpRequest req, string id)
        {
            return Ok(
                () => (new DatabaseRepo<ProjectTemplate>()).Delete(id),
                (p) => new
                {
                    deletedProjectTemplate = p
                });
        }


        [Allow(Role.Admin)]
        [Function("GetProjectTemplate")]
        public static async Task<IActionResult> RunGetProjectTemplate(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projectTemplate/{id}/")] HttpRequest req, string id)
        {
            return Ok(
                () => (new DatabaseRepo<ProjectTemplate>()).Get(id),
                (p) => new
                {
                    projectTemplate = p
                });
        }


        [Allow(Role.Admin)]
        [Function("UpdateProjectTemplate")]
        public static async Task<IActionResult> RunUpdateProjectTemplate(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projectTemplate/{id}/update/")] HttpRequest req, string id)
        {
            ProjectTemplate projectTemplate = JsonConvert.DeserializeObject<ProjectTemplate>(
                await new StreamReader(req.Body).ReadToEndAsync());
            projectTemplate.Id = id;

            return Ok(
                () => (new DatabaseRepo<ProjectTemplate>()).Update(projectTemplate),
                (res) => new
                {
                    oldProjectTemplate = ((Tuple<ProjectTemplate, ProjectTemplate>)res).Item1,
                    newProjectTemplate = ((Tuple<ProjectTemplate, ProjectTemplate>)res).Item2,
                });
        }
    }
}
