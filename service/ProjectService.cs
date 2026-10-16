
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class ProjectService : IProjectService
	{

		private IProjectRepo _repo;
		private ISecurityService _securityService;
		private IProjectAssignmentRepo _assignmentRepo;
		private ITeamMemberRepo _teamMemberRepo;
		private readonly DatabaseRepo<ProjectRole> _roleRepo = new DatabaseRepo<ProjectRole>();

		public ProjectService(
			IProjectRepo repo,
			ISecurityService securityService,
			IProjectAssignmentRepo assignmentRepo,
			ITeamMemberRepo teamMemberRepo)
		{
			_repo = repo;
			_securityService = securityService;
			_assignmentRepo = assignmentRepo;
			_teamMemberRepo = teamMemberRepo;
		}

		// Visibility is project-level even though assignments are made against
		// sub-tasks: one assignment anywhere in a project reveals the whole project.
		// Pruning sub-tasks here too would render a work breakdown full of holes
		// that nobody could tell apart from the real plan. What you may *book time
		// against* stays sub-task-level - see TimeEntryService.MayLogAgainst.
		//
		// Returns null when everything is visible, which is the admin case. That
		// spares callers a set lookup per row on the common path.
		private HashSet<string> AssignedProjectIds()
		{
			if (_securityService.IsCurrentUserAdmin())
			{
				return null;
			}

			string teamMemberId = _securityService.GetCurrentTeamMemberId();

			if (string.IsNullOrWhiteSpace(teamMemberId))
			{
				return new HashSet<string>();
			}

			// A project manager sees the projects they manage even when they are
			// not assigned to work on them.
			HashSet<string> visible = _assignmentRepo
				.GetByTeamMember(teamMemberId)
				.Select(a => a.ProjectId)
				.ToHashSet();
			if (!TestingAsEmployee())
			{
				visible.UnionWith(ManagedBy(teamMemberId));
			}
			return visible;
		}

		// Testing as an employee on the sandbox: managing nothing, whatever the
		// projects say, so no PM views open.
		private bool TestingAsEmployee()
		{
			return _securityService.TestRole() == SandboxRoles.Employee;
		}

		private HashSet<string> ManagedBy(string teamMemberId)
		{
			return _repo
				.GetAll()
				.Where(p => !string.IsNullOrWhiteSpace(p.ProjectManagerId) &&
					p.ProjectManagerId == teamMemberId)
				.Select(p => p.Id)
				.ToHashSet();
		}

		// The projects the caller is Project Manager of (Project.ProjectManagerId).
		// Being PM gives read-only access to everyone's time on those projects.
		// Empty for someone who manages nothing, admins included - admins get
		// their access from being admin, not from this.
		public HashSet<string> ManagedProjectIds()
		{
			string teamMemberId = _securityService.GetCurrentTeamMemberId();

			return string.IsNullOrWhiteSpace(teamMemberId) || TestingAsEmployee()
				? new HashSet<string>()
				: ManagedBy(teamMemberId);
		}

		public bool MayView(string projectId)
		{
			HashSet<string> visible = AssignedProjectIds();
			return visible == null || visible.Contains(projectId);
		}

		// Tasks and sub-tasks hang off a project and so inherit its visibility.
		// Null means "no restriction".
		public HashSet<string> VisibleProjectIds()
		{
			return AssignedProjectIds();
		}

		public Project Create(Project newProject)
		{
			_securityService.AuthorizeUser();
			return _repo.Create(newProject);
		}

		// The rules for saving a project, shared by the website's endpoints and
		// Claude's create_project tool. Returns null when the project is valid,
		// otherwise the reason it isn't. Also tidies what it checks: trims text,
		// and derives Billable from the project type so the two can't disagree.
		public string Validate(Project project)
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

			project.Status = string.IsNullOrWhiteSpace(project.Status) ? null : project.Status.Trim();
			if (project.Status != null && !Project.Statuses.Contains(project.Status))
			{
				return "Status must be In process, Finished, Cancelled or SMA.";
			}

			project.Name = project.Name?.Trim();
			project.FundingCode = project.FundingCode?.Trim();
			project.Division = project.Division?.Trim();
			project.PoNumber = project.PoNumber?.Trim();
			project.Billable = project.ProjectType != Project.TypeNonBillable;
			return null;
		}

		public Project CreateWithManager(Project project)
		{
			Project created = Create(project);
			AddManagerToTeam(created);
			return created;
		}

		// Naming someone Project Manager puts them on the project's team, as
		// Project Manager, if they aren't on it already.
		//
		// Someone already on the team keeps the role they have: the role sets
		// their billing rate, so it isn't changed behind the admin's back.
		// Replacing the PM doesn't take the old one off the team either - time
		// they've logged still needs their role to be priced.
		public void AddManagerToTeam(Project project)
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

		public Project Update(Project project)
		{
			_securityService.AuthorizeUser();
			return _repo.Update(project).Item2;
		}

		public Project Get(string id)
		{
			_securityService.AuthorizeUser();
			return _repo.Get(id);
		}

		public List<Project> GetAllProjects()
		{
			_securityService.AuthorizeUser();

			List<Project> all = _repo.GetAll();
			HashSet<string> visible = AssignedProjectIds();

			return visible == null
				? all
				: all.Where(p => visible.Contains(p.Id)).ToList();
		}
	}
}
