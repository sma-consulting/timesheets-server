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
        private readonly IProjectAssignmentRepo _assignmentRepo;
        private readonly ITeamMemberRepo _teamMemberRepo;
        private readonly DatabaseRepo<ProjectRole> _roleRepo = new DatabaseRepo<ProjectRole>();
        private readonly ILogger<ProjectConfigFunctions> _logger;

        public ProjectConfigFunctions(
            IProjectService projectService,
            ISecurityService securityService,
            IProjectAssignmentRepo assignmentRepo,
            ITeamMemberRepo teamMemberRepo,
            ILogger<ProjectConfigFunctions> logger)
        {
            _projectService = projectService;
            _securityService = securityService;
            _assignmentRepo = assignmentRepo;
            _teamMemberRepo = teamMemberRepo;
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
                () =>
                {
                    Project created = _projectService.Create(project);
                    AddManagerToTeam(created);
                    return created;
                },
                (p) => new
                {
                    project = p
                });
        }


        [FunctionName("DeleteProject")]
        public async Task<IActionResult> RunDeleteProject(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "project/{id}/")] HttpRequest req, string id)
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
                () =>
                {
                    Project updated = _projectService.Update(project);
                    AddManagerToTeam(project);
                    return updated;
                },
                (res) => new
                {
                    newProject = res
                });
        }


        // Naming someone Project Manager puts them on the project's team, as
        // Project Manager, if they aren't on it already.
        //
        // Someone already on the team keeps the role they have: the role sets
        // their billing rate, so it isn't changed behind the admin's back.
        // Replacing the PM doesn't take the old one off the team either - time
        // they've logged still needs their role to be priced.
        private void AddManagerToTeam(Project project)
        {
            string managerId = project?.ProjectManagerId;
            if (string.IsNullOrWhiteSpace(managerId) || string.IsNullOrWhiteSpace(project.Id))
            {
                return;
            }
            // Also skips the "Select project manager..." placeholder, which
            // isn't a real person.
            if (_teamMemberRepo.Get(managerId) == null)
            {
                return;
            }
            if (_assignmentRepo.GetByProject(project.Id).Any(a => a.TeamMemberId == managerId))
            {
                return;
            }

            _assignmentRepo.Create(new ProjectAssignment
            {
                ProjectId = project.Id,
                TeamMemberId = managerId,
                ProjectRoleId = ProjectManagerRole().Id,
                StartDate = project.StartDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            });
        }

        // The Project Manager role from Settings - matched by its code (PM) or
        // its name - or, if there isn't one yet, a new one.
        private ProjectRole ProjectManagerRole()
        {
            ProjectRole role = _roleRepo.GetAll().FirstOrDefault(r =>
                string.Equals(r.Code?.Trim(), "PM", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.Name?.Trim(), "Project Manager", StringComparison.OrdinalIgnoreCase));

            return role ?? _roleRepo.Create(new ProjectRole { Name = "Project Manager", Code = "PM" });
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
