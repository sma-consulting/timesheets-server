using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace sma.plan
{
    // Roles are global - one "Developer" shared by every project. Anyone signed in
    // may read them, since every assign dropdown needs the list. Only admins may
    // change them.
    internal class ProjectRoleConfigFunctions : RequestHandler
    {
        private readonly ILogger<ProjectRoleConfigFunctions> _logger;
        private readonly ISecurityService _securityService;
        private readonly IProjectAssignmentRepo _assignmentRepo;
        private readonly ITeamMemberRepo _teamMemberRepo;
        private readonly IProjectBillingRateRepo _rateRepo;
        private readonly DatabaseRepo<ProjectRole> _roleRepo = new DatabaseRepo<ProjectRole>();

        public ProjectRoleConfigFunctions(
            ILogger<ProjectRoleConfigFunctions> logger,
            ISecurityService securityService,
            IProjectAssignmentRepo assignmentRepo,
            ITeamMemberRepo teamMemberRepo,
            IProjectBillingRateRepo rateRepo)
        {
            _logger = logger;
            _securityService = securityService;
            _assignmentRepo = assignmentRepo;
            _teamMemberRepo = teamMemberRepo;
            _rateRepo = rateRepo;
        }

        [FunctionName("CreateProjectRole")]
        public async Task<IActionResult> RunCreateProjectRole(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projectRole/create/")] HttpRequest req)
        {
            if (!_securityService.IsCurrentUserAdmin())
            {
                return Forbidden();
            }

            ProjectRole projectRole = JsonConvert.DeserializeObject<ProjectRole>(
                await new StreamReader(req.Body).ReadToEndAsync());

            string invalid = Validate(projectRole, null);
            if (invalid != null)
            {
                return Invalid(invalid);
            }

            return Ok(
                () => _roleRepo.Create(projectRole),
                (p) => new
                {
                    projectRole = p
                });
        }


        // Refused while the role is in use - deleting it would leave those
        // assignments, team members and billing rates pointing at a role that no
        // longer exists.
        [FunctionName("DeleteProjectRole")]
        public async Task<IActionResult> RunDeleteProjectRole(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "projectRole/{id}/delete/")] HttpRequest req, string id)
        {
            if (!_securityService.IsCurrentUserAdmin())
            {
                return Forbidden();
            }

            int assignments = _assignmentRepo.GetAll().Count(a => a.ProjectRoleId == id);
            int teamMembers = _teamMemberRepo.GetAll()
                .Count(t => t.AssignedRoles != null && t.AssignedRoles.Contains(id));

            int rates = _rateRepo.GetAll().Count(r => r.ProjectRoleId == id);

            if (assignments > 0 || teamMembers > 0 || rates > 0)
            {
                return Invalid(string.Format(
                    "This role is still in use by {0} project assignment(s), {1} team member(s) and {2} billing rate(s). Remove those first.",
                    assignments, teamMembers, rates));
            }

            return Ok(
                () => _roleRepo.Delete(id),
                (p) => new
                {
                    deletedProjectRole = p
                });
        }


        [FunctionName("GetProjectRole")]
        public async Task<IActionResult> RunGetProjectRole(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projectRole/{id}/")] HttpRequest req, string id)
        {
            return Ok(
                () => _roleRepo.Get(id),
                (p) => new
                {
                    projectRole = p
                });
        }


        [FunctionName("GetAllProjectRole")]
        public async Task<IActionResult> RunGetAllProjectRole(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projectRole/")] HttpRequest req)
        {
            return Ok(
                () => _roleRepo.GetAll(),
                (p) => new
                {
                    projectRoleList = p
                });
        }


        [FunctionName("UpdateProjectRole")]
        public async Task<IActionResult> RunUpdateProjectRole(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projectRole/{id}/update/")] HttpRequest req, string id)
        {
            if (!_securityService.IsCurrentUserAdmin())
            {
                return Forbidden();
            }

            ProjectRole projectRole = JsonConvert.DeserializeObject<ProjectRole>(
                await new StreamReader(req.Body).ReadToEndAsync());
            projectRole.Id = id;

            string invalid = Validate(projectRole, id);
            if (invalid != null)
            {
                return Invalid(invalid);
            }

            return Ok(
                () => _roleRepo.Update(projectRole),
                (res) => new
                {
                    oldProjectRole = ((Tuple<ProjectRole, ProjectRole>)res).Item1,
                    newProjectRole = ((Tuple<ProjectRole, ProjectRole>)res).Item2,
                });
        }


        // Names are unique ignoring case, so "Developer" and "developer" can't both
        // exist - roles are picked by name from a dropdown, and two that look the
        // same would split one kind of work across two rows. `selfId` lets a role
        // keep its own name when renamed.
        private string Validate(ProjectRole role, string selfId)
        {
            if (role == null || string.IsNullOrWhiteSpace(role.Name))
            {
                return "A role name is required.";
            }

            role.Name = role.Name.Trim();
            role.Code = string.IsNullOrWhiteSpace(role.Code) ? null : role.Code.Trim();

            bool taken = _roleRepo.GetAll().Any(r =>
                r.Id != selfId &&
                string.Equals(r.Name?.Trim(), role.Name, StringComparison.OrdinalIgnoreCase));

            return taken
                ? string.Format("A role called \"{0}\" already exists.", role.Name)
                : null;
        }
    }
}
