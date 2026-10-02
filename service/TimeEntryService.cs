using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class TimeEntryService : ITimeEntryService
	{
		private ITimeEntryRepo _repo;
		private ISecurityService _securityService;
		private IProjectAssignmentRepo _assignmentRepo;
		private IProjectSubTaskRepo _subTaskRepo;
		private IProjectTaskRepo _taskRepo;
		private IProjectService _projectService;

		public TimeEntryService(
			ITimeEntryRepo repo,
			ISecurityService securityService,
			IProjectAssignmentRepo assignmentRepo,
			IProjectSubTaskRepo subTaskRepo,
			IProjectTaskRepo taskRepo,
			IProjectService projectService)
		{
			_repo = repo;
			_securityService = securityService;
			_assignmentRepo = assignmentRepo;
			_subTaskRepo = subTaskRepo;
			_taskRepo = taskRepo;
			_projectService = projectService;
		}

		// Assignment is project-level: being put on a project lets you book time
		// against any of its sub-tasks, and you pick which one yourself. So this
		// resolves the sub-task up to its project and checks the assignment there.
		// Admins are exempt so they can correct anyone's timesheet.
		public bool MayLogAgainst(string teamMemberId, string projectSubTaskId)
		{
			if (_securityService.IsCurrentUserAdmin())
			{
				return true;
			}

			if (string.IsNullOrWhiteSpace(teamMemberId) ||
				string.IsNullOrWhiteSpace(projectSubTaskId))
			{
				return false;
			}

			ProjectSubTask subTask = _subTaskRepo.Get(projectSubTaskId);

			if (subTask == null)
			{
				return false;
			}

			// ProjectId is denormalised onto the sub-task and is missing on older
			// records, so fall back to the parent task, which always has it.
			string projectId = subTask.ProjectId;

			if (string.IsNullOrWhiteSpace(projectId) &&
				!string.IsNullOrWhiteSpace(subTask.ProjectTaskId))
			{
				projectId = _taskRepo.Get(subTask.ProjectTaskId)?.ProjectId;
			}

			if (string.IsNullOrWhiteSpace(projectId))
			{
				return false;
			}

			return _assignmentRepo
				.GetByTeamMember(teamMemberId)
				.Any(a => a.ProjectId == projectId);
		}

		// Returns null when the entry is valid, otherwise the reason it isn't.
		// Applies to admins too: being an admin exempts you from the assignment
		// rule, not from recording sensible data.
		//
		// The sub-task is the authoritative end of the chain, so a blank project
		// or task id is filled in from it, but one that contradicts it is rejected
		// rather than silently corrected - that usually means a stale client.
		public string Validate(TimeEntry timeEntry)
		{
			if (timeEntry == null)
			{
				return "No time entry was sent.";
			}

			if (timeEntry.Date == default)
			{
				return "A date is required.";
			}

			if (timeEntry.Hours <= 0 || timeEntry.Hours > 24)
			{
				return "Hours must be more than 0 and at most 24.";
			}

			// Quarter, half or whole hours only. decimal keeps this exact - no
			// floating-point fuzz deciding whether 0.75 is a multiple of 0.25.
			if (timeEntry.Hours * 4 != decimal.Truncate(timeEntry.Hours * 4))
			{
				return "Hours must be in quarter-hour steps (e.g. 1, 1.25, 1.5, 1.75).";
			}

			if (string.IsNullOrWhiteSpace(timeEntry.ProjectSubTaskId))
			{
				return "Select a project, subproject and task.";
			}

			ProjectSubTask subTask = _subTaskRepo.Get(timeEntry.ProjectSubTaskId);

			if (subTask == null)
			{
				return "That task no longer exists.";
			}

			ProjectTask task = string.IsNullOrWhiteSpace(subTask.ProjectTaskId)
				? null
				: _taskRepo.Get(subTask.ProjectTaskId);

			if (task == null)
			{
				return "That task is not attached to a subproject.";
			}

			string projectId = string.IsNullOrWhiteSpace(subTask.ProjectId)
				? task.ProjectId
				: subTask.ProjectId;

			if (!string.IsNullOrWhiteSpace(timeEntry.ProjectTaskId) &&
				timeEntry.ProjectTaskId != task.Id)
			{
				return "That task does not belong to the selected subproject.";
			}

			if (!string.IsNullOrWhiteSpace(timeEntry.ProjectId) &&
				timeEntry.ProjectId != projectId)
			{
				return "That task does not belong to the selected project.";
			}

			timeEntry.ProjectTaskId = task.Id;
			timeEntry.ProjectId = projectId;
			return null;
		}

		// Non-admins always own what they write, whatever the client sent.
		public string OwnerFor(TimeEntry timeEntry)
		{
			var callerTeamMemberId = _securityService.GetCurrentTeamMemberId();

			if (!_securityService.IsCurrentUserAdmin())
			{
				return callerTeamMemberId;
			}

			return string.IsNullOrWhiteSpace(timeEntry.TeamMemberId)
				? callerTeamMemberId
				: timeEntry.TeamMemberId;
		}

		// A new entry starts its audit trail clean, whatever the client sent, so
		// the first snapshot an update takes records who created it and when.
		public TimeEntry Create(TimeEntry timeEntry)
		{
			_securityService.AuthorizeUser();
			timeEntry.TeamMemberId = OwnerFor(timeEntry);
			timeEntry.History = new List<TimeEntry>();
			timeEntry.LastModified = DateTime.UtcNow;
			timeEntry.ModifiedBy = _securityService.GetCurrentTeamMemberId();
			return _repo.Create(timeEntry);
		}

		// The update replaces the whole stored entry, and the client never sends
		// the audit fields - so they're carried over from what's stored rather
		// than taken from the request, then the old version goes onto History.
		public TimeEntry Update(TimeEntry timeEntry)
		{
			_securityService.AuthorizeUser();
			timeEntry.TeamMemberId = OwnerFor(timeEntry);

			TimeEntry existing = _repo.Get(timeEntry.Id);
			timeEntry.History = existing.History ?? new List<TimeEntry>();
			timeEntry.LastModified = existing.LastModified;
			timeEntry.ModifiedBy = existing.ModifiedBy;

			if (!timeEntry.SameAs(existing))
			{
				timeEntry.History.Add(existing.AuditClone());
				timeEntry.LastModified = DateTime.UtcNow;
				timeEntry.ModifiedBy = _securityService.GetCurrentTeamMemberId();
			}

			return _repo.Update(timeEntry).Item2;
		}

		// Guards updates and deletes of an entry that already exists: only its owner
		// or an admin. Without this an id is enough to overwrite or delete a
		// colleague's entry - and on an update OwnerFor would quietly reassign it to
		// whoever called. Project Managers do not pass this; see MayRead.
		public bool MayWrite(string timeEntryId)
		{
			if (_securityService.IsCurrentUserAdmin())
			{
				return true;
			}

			if (string.IsNullOrWhiteSpace(timeEntryId))
			{
				return false;
			}

			TimeEntry existing = _repo.Get(timeEntryId);

			// A missing entry is not an access failure; let the repo report it.
			if (existing == null)
			{
				return true;
			}

			string callerTeamMemberId = _securityService.GetCurrentTeamMemberId();

			return !string.IsNullOrWhiteSpace(callerTeamMemberId)
				&& existing.TeamMemberId == callerTeamMemberId;
		}

		public TimeEntry Get(string id)
		{
			_securityService.AuthorizeUser();
			return _repo.Get(id);
		}

		public TimeEntry Delete(string id)
		{
			_securityService.AuthorizeUser();
			return _repo.Delete(id);
		}

		// Reading is wider than writing: a Project Manager may read - never
		// change - anyone's time on a project they manage.
		public bool MayRead(string timeEntryId)
		{
			if (MayWrite(timeEntryId))
			{
				return true;
			}

			TimeEntry existing = _repo.Get(timeEntryId);

			return existing != null
				&& !string.IsNullOrWhiteSpace(existing.ProjectId)
				&& _projectService.ManagedProjectIds().Contains(existing.ProjectId);
		}

		// Admins see every timesheet so they can review and correct them. Everyone
		// else sees their own, plus - for Project Managers - their projects'
		// entries. An unresolved caller sees nothing rather than everything -
		// failing closed matters more here than a helpful fallback.
		public List<TimeEntry> GetAllTimeEntries()
		{
			_securityService.AuthorizeUser();

			List<TimeEntry> all = _repo.GetAll();

			if (_securityService.IsCurrentUserAdmin())
			{
				return all;
			}

			string callerTeamMemberId = _securityService.GetCurrentTeamMemberId();

			if (string.IsNullOrWhiteSpace(callerTeamMemberId))
			{
				return new List<TimeEntry>();
			}

			// Project Managers also get everyone's entries on the projects they
			// manage, so they can review them. MayWrite keeps those read-only.
			HashSet<string> managed = _projectService.ManagedProjectIds();

			return all
				.Where(t => t.TeamMemberId == callerTeamMemberId ||
					(!string.IsNullOrWhiteSpace(t.ProjectId) && managed.Contains(t.ProjectId)))
				.ToList();
		}
	}
}
