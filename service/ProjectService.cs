
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

		public ProjectService(
			IProjectRepo repo,
			ISecurityService securityService,
			IProjectAssignmentRepo assignmentRepo)
		{
			_repo = repo;
			_securityService = securityService;
			_assignmentRepo = assignmentRepo;
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
			visible.UnionWith(ManagedBy(teamMemberId));
			return visible;
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

			return string.IsNullOrWhiteSpace(teamMemberId)
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
