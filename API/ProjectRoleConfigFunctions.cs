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
    internal class ProjectRoleConfigFunctions : RequestHandler
    {

        private ILogger<ProjectRoleConfigFunctions> _logger;


		public ProjectRoleConfigFunctions(ILogger<ProjectRoleConfigFunctions> logger)
        {
            _logger = logger;
        }

        [FunctionName("CreateProjectRole")]
        public static async Task<IActionResult> RunCreateProjectRole(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projectRole/create/")] HttpRequest req)
        {
            ProjectRole projectRole = JsonConvert.DeserializeObject<ProjectRole>(
                await new StreamReader(req.Body).ReadToEndAsync());

            return Ok(
                () => (new DatabaseRepo<ProjectRole>()).Create(projectRole),
                (p) => new
                {
                    projectRole = p
                });
        }


        [FunctionName("DeleteProjectRole")]
        public static async Task<IActionResult> RunDeleteProjectRole(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "projectRole/{id}/delete/")] HttpRequest req, string id)
        {
            return Ok(
                () => (new DatabaseRepo<ProjectRole>()).Delete(id),
                (p) => new
                {
                    deletedProjectRole = p
                });
        }


        [FunctionName("GetProjectRole")]
        public static async Task<IActionResult> RunGetProjectRole(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projectRole/{id}/")] HttpRequest req, string id)
        {
            return Ok(
                () => (new DatabaseRepo<ProjectRole>()).Get(id),
                (p) => new
                {
                    projectRole = p
                });
        }


        [FunctionName("UpdateProjectRole")]
        public static async Task<IActionResult> RunUpdateProjectRole(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projectRole/{id}/update/")] HttpRequest req, string id)
        {
            ProjectRole projectRole = JsonConvert.DeserializeObject<ProjectRole>(
                await new StreamReader(req.Body).ReadToEndAsync());
            projectRole.Id = id;

            return Ok(
                () => (new DatabaseRepo<ProjectRole>()).Update(projectRole),
                (res) => new
                {
                    oldProjectRole = ((Tuple<ProjectRole, ProjectRole>)res).Item1,
                    newProjectRole = ((Tuple<ProjectRole, ProjectRole>)res).Item2,
                });
        }
    }
}
