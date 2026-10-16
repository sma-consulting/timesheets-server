using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace sma.plan
{
    // Tools for Claude (MCP). Each [Function] with an [McpToolTrigger] is one
    // tool: Claude reads its name and description to decide when to use it.
    internal class McpTools
    {

        // The most tasks find_my_work lists, so a vague search doesn't flood Claude.
        private const int MaxWorkResults = 30;

        // The longest stretch list_my_time covers in one go, for the same reason.
        private const int MaxListDays = 62;

        private const string NotIdentified =
            "I can't tell who you are, so I can't do this for you. Sign in to the connector again, or ask an admin to link your login to a team member.";

        private const string NotOnTeam =
            "You're not on that project's team, so you can't log time to it. Ask its project manager or an admin to add you.";

        private const string NoSuchEntry =
            "You have no time entry with that id. Use list_my_time to find it.";

        private readonly ICustomerRepo _customerRepo;
        private readonly ITeamMemberRepo _teamMemberRepo;
        private readonly IProjectService _projectService;
        private readonly ISecurityService _securityService;
        private readonly ITimeEntryService _timeEntryService;
        private readonly IProjectAssignmentRepo _assignmentRepo;
        private readonly IProjectTaskRepo _taskRepo;
        private readonly IProjectSubTaskRepo _subTaskRepo;

        public McpTools(
            ICustomerRepo customerRepo,
            ITeamMemberRepo teamMemberRepo,
            IProjectService projectService,
            ISecurityService securityService,
            ITimeEntryService timeEntryService,
            IProjectAssignmentRepo assignmentRepo,
            IProjectTaskRepo taskRepo,
            IProjectSubTaskRepo subTaskRepo)
        {
            _customerRepo = customerRepo;
            _teamMemberRepo = teamMemberRepo;
            _projectService = projectService;
            _securityService = securityService;
            _timeEntryService = timeEntryService;
            _assignmentRepo = assignmentRepo;
            _taskRepo = taskRepo;
            _subTaskRepo = subTaskRepo;
        }

        [Allow(Role.Anyone)]
        [Function(nameof(McpHello))]
        public string McpHello(
            [McpToolTrigger("hello", "Says hello. Use it to check the SMA timesheets connector works.")]
                ToolInvocationContext context,
            [McpToolProperty("name", "Who to greet.", isRequired: true)]
                string name)
        {
            // Also says who the caller is signed in as, to check sign-in works.
            string email = _securityService.GetCurrentEmail();
            string who = email == null ? "I can't tell who you're signed in as." : $"You're signed in as {email}.";
            return $"Hello {name}, this is the SMA timesheets server. {who}";
        }

        [Allow(Role.Anyone)]
        [Function(nameof(McpFindClient))]
        public object McpFindClient(
            [McpToolTrigger("find_client", "Finds clients whose name contains the text. Use it to get a client's id before creating a project. If several match, ask the user which one.")]
                ToolInvocationContext context,
            [McpToolProperty("name", "Part of the client's name, e.g. 'VLW'.", isRequired: true)]
                string name)
            
        {
            return _customerRepo.GetAll()
				    .Where(c => (c.Name ?? "").Contains(name, StringComparison.OrdinalIgnoreCase))
				    .Select(c => new { c.Id, c.Name })
				    .ToList();
        }
        [Allow(Role.Anyone)]
        [Function(nameof(McpFindTeamMember))]
        public object McpFindTeamMember(
            [McpToolTrigger("find_team_member", "Finds team members whose name contains the text. Use it to get a person's id, e.g. for a project manager. If several match, ask the user which one.")]
                ToolInvocationContext context,
            [McpToolProperty("name", "Part of the person's name, e.g. 'Andriy'.", isRequired: true)]
                string name)
        {
            return _teamMemberRepo.GetAll()
                .Where(m => (m.Name ?? "").Contains(name, StringComparison.OrdinalIgnoreCase))
                .Select(m => new { m.Id, m.Name })
                .ToList();
        }

        [Allow(Role.Anyone)]
        [Function(nameof(McpCreateClient))]
        public string McpCreateClient(
            [McpToolTrigger("create_client", "Creates a new client. Admins only. Use it only when find_client finds no match, and confirm the name with the user first, so the same client isn't added twice.")]
                ToolInvocationContext context,
            [McpToolProperty("name", "The client's full name, e.g. 'VLW Engineering Ltd.'.", isRequired: true)]
                string name,
            [McpToolProperty("code", "A short reference used on invoices, e.g. 'VLW'.")]
                string code,
            [McpToolProperty("currency", "3-letter currency code. CAD when not said.")]
                string currency)
        {
            // Only admins create clients, as on the website. (MCP calls skip
            // SecurityMiddleware, so the tool checks for itself.)
            if (!_securityService.IsCurrentUserAdmin())
            {
                return "Only admins can create clients.";
            }

            // Claude might miss that the client exists: an exact name match
            // returns the existing one instead of a duplicate.
            Customer existing = _customerRepo.GetAll().FirstOrDefault(c =>
                string.Equals(c.Name?.Trim(), name?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                return $"A client named '{existing.Name}' already exists (id {existing.Id}). Use that one.";
            }

            // Checked and saved by the same rules as the website.
            Customer customer = new Customer { Name = name, Code = code, Currency = currency };
            string invalid = Customer.Validate(customer);
            if (invalid != null)
            {
                return invalid;
            }

            Customer created = _customerRepo.Create(customer);
            return $"Created client '{created.Name}' (id {created.Id}).";
        }

        [Allow(Role.Anyone)]
        [Function(nameof(McpCreateProject))]
        public string McpCreateProject(
    [McpToolTrigger("create_project", "Creates a project. Admins only. First use find_client for the client's id and find_team_member for the project manager's id. If the client doesn't exist yet, offer to create it with create_client. Ask the user for anything missing, and confirm the details with them before calling this.")]
                ToolInvocationContext context,
    [McpToolProperty("name", "The project's name.", isRequired: true)]
                string name,
    [McpToolProperty("clientId", "The client's id, from find_client.", isRequired: true)]
                string clientId,
    [McpToolProperty("projectManagerId", "The project manager's id, from find_team_member.")]
                string projectManagerId,
    [McpToolProperty("projectCode", "The SMA project code, e.g. 24-017.")]
                string projectCode,
    [McpToolProperty("projectType", "How the client is charged: Hourly, LumpSum or NonBillable. Hourly when not said.")]
                string projectType,
    [McpToolProperty("startDate", "The start date, written yyyy-MM-dd.")]
                string startDate,
    [McpToolProperty("endDate", "The end date, written yyyy-MM-dd.")]
                string endDate,
    [McpToolProperty("division", "The division the project belongs to.")]
                string division,
    [McpToolProperty("poNumber", "The client's purchase order number.")]
                string poNumber)
        {
            // 1. Only admins create projects, as on the website. (MCP calls
            // skip SecurityMiddleware, so the tool checks for itself.)
            if (!_securityService.IsCurrentUserAdmin())
            {
                return "Only admins can create projects.";
            }

            // 2. The ids must be real: a made-up one would break the save.
            if (string.IsNullOrWhiteSpace(name))
            {
                return "The project needs a name.";
            }
            if (!_customerRepo.GetAll().Any(c => c.Id == clientId))
            {
                return "There's no client with that id. Use find_client to look it up.";
            }
            if (!string.IsNullOrWhiteSpace(projectManagerId) &&
                !_teamMemberRepo.GetAll().Any(m => m.Id == projectManagerId))
            {
                return "There's no team member with that id. Use find_team_member to look it up.";
            }

            // 3. Dates arrive as text.
            if (!TryReadDate(startDate, out DateOnly? start) || !TryReadDate(endDate, out DateOnly? end))
            {
                return "Dates must be written yyyy-MM-dd, e.g. 2026-11-01.";
            }

            // 4. Checked and saved by the same rules as the website.
            Project project = new Project
            {
                Name = name,
                CustomerId = clientId,
                ProjectManagerId = string.IsNullOrWhiteSpace(projectManagerId) ? null : projectManagerId,
                FundingCode = projectCode,
                ProjectType = projectType,
                StartDate = start,
                EndDate = end,
                Division = division,
                PoNumber = poNumber,
            };

            string invalid = _projectService.Validate(project);
            if (invalid != null)
            {
                return invalid;
            }

            Project created = _projectService.CreateWithManager(project);
            return $"Created project '{created.Name}' (id {created.Id}).";
        }

        // The tasks the caller can log time to, matching every word searched
        // for in the project's name or code, the subproject or the task. In
        // the app a project has subprojects (ProjectTask) and those have
        // tasks (ProjectSubTask); time is logged against a task.
        [Allow(Role.Anyone)]
        [Function(nameof(McpFindMyWork))]
        public object McpFindMyWork(
            [McpToolTrigger("find_my_work", "Finds the tasks the signed-in user can log time to, matching words in the project's name or code, the subproject or the task, e.g. 'VLW design'. Use it to get a task's subTaskId before log_time. If several match, ask the user which one.")]
                ToolInvocationContext context,
            [McpToolProperty("search", "Words to match, e.g. 'VLW design'. Leave empty to list everything the user is on.")]
                string search)
        {
            string me = _securityService.GetCurrentTeamMemberId();
            if (me == null)
            {
                return NotIdentified;
            }

            // The projects they can log to: the ones they're on, or any for an
            // admin - the same rule as the Time Log (TimeEntryService.MayLogAgainst).
            bool admin = _securityService.IsCurrentUserAdmin();
            HashSet<string> onTeam = _assignmentRepo.GetByTeamMember(me).Select(a => a.ProjectId).ToHashSet();
            Dictionary<string, Project> projects = _projectService.GetAllProjects()
                .Where(p => admin || onTeam.Contains(p.Id))
                .ToDictionary(p => p.Id);
            Dictionary<string, ProjectTask> subprojects = _taskRepo.GetAll().ToDictionary(t => t.Id);

            string[] words = (search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var matches = _subTaskRepo.GetAll()
                .Select(task =>
                {
                    ProjectTask subproject = task.ProjectTaskId != null && subprojects.TryGetValue(task.ProjectTaskId, out ProjectTask s) ? s : null;
                    // ProjectId is missing on older tasks; their subproject has it.
                    string projectId = string.IsNullOrWhiteSpace(task.ProjectId) ? subproject?.ProjectId : task.ProjectId;
                    Project project = projectId != null && projects.TryGetValue(projectId, out Project p) ? p : null;
                    return new { task, subproject, project };
                })
                .Where(m => m.project != null && m.subproject != null)
                .Where(m => words.All(w =>
                    $"{m.project.Name} {m.project.FundingCode} {m.subproject.Name} {m.task.Name}"
                        .Contains(w, StringComparison.OrdinalIgnoreCase)))
                .Select(m => new
                {
                    subTaskId = m.task.Id,
                    project = string.IsNullOrWhiteSpace(m.project.FundingCode)
                        ? m.project.Name
                        : $"{m.project.FundingCode} · {m.project.Name}",
                    subproject = m.subproject.Name,
                    task = m.task.Name,
                })
                .Take(MaxWorkResults + 1)
                .ToList();

            if (matches.Count > MaxWorkResults)
            {
                return new
                {
                    tasks = matches.Take(MaxWorkResults),
                    note = $"More than {MaxWorkResults} tasks match. Ask the user which project or task they mean, and search again with more words.",
                };
            }

            return matches;
        }

        // Logs the caller's own hours, through the Time Log's own rules
        // (TimeEntryService): quarter-hours up to 24, only on projects they're
        // on, and the entry is theirs whatever is sent.
        [Allow(Role.Anyone)]
        [Function(nameof(McpLogTime))]
        public string McpLogTime(
            [McpToolTrigger("log_time", "Logs the signed-in user's own hours against a task. First use find_my_work to get the task's subTaskId. Work out dates like 'yesterday' from today's date, and confirm the date, task and hours with the user before logging. For several tasks or days, call it once per entry.")]
                ToolInvocationContext context,
            [McpToolProperty("subTaskId", "The task's subTaskId, from find_my_work.", isRequired: true)]
                string subTaskId,
            [McpToolProperty("date", "The day worked, written yyyy-MM-dd.", isRequired: true)]
                string date,
            [McpToolProperty("hours", "Hours worked, in quarter-hour steps, e.g. 1.5 or 0.25.", isRequired: true)]
                string hours,
            [McpToolProperty("notes", "What was done, if the user said.")]
                string notes)
        {
            string me = _securityService.GetCurrentTeamMemberId();
            if (me == null)
            {
                return NotIdentified;
            }

            // The id must be real: a made-up one would break the save.
            if (string.IsNullOrWhiteSpace(subTaskId) || !_subTaskRepo.GetAll().Any(s => s.Id == subTaskId))
            {
                return "There's no task with that id. Use find_my_work to look it up.";
            }
            if (!TryReadDate(date, out DateOnly? day) || day == null)
            {
                return "The date must be written yyyy-MM-dd, e.g. 2026-10-15.";
            }
            // Hours arrive as text, so "1.5" can't be misread as a date or by
            // the server's language settings.
            if (!decimal.TryParse(hours?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount))
            {
                return "Hours must be a number, e.g. 1.5.";
            }

            TimeEntry entry = new TimeEntry
            {
                ProjectSubTaskId = subTaskId,
                TeamMemberId = me,
                Date = day.Value,
                Hours = amount,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            };

            // Checked and saved by the same rules as the Time Log.
            string invalid = _timeEntryService.Validate(entry);
            if (invalid != null)
            {
                return invalid;
            }
            if (!_timeEntryService.MayLogAgainst(_timeEntryService.OwnerFor(entry), entry.ProjectSubTaskId))
            {
                return NotOnTeam;
            }

            TimeEntry saved = _timeEntryService.Create(entry);
            return $"Logged {HoursText(saved.Hours)}h on {Describe(saved, WorkNames())} for {DayText(saved.Date)} (entry id {saved.Id}).";
        }

        // The caller's own entries between two days, by day, with totals.
        [Allow(Role.Anyone)]
        [Function(nameof(McpListMyTime))]
        public object McpListMyTime(
            [McpToolTrigger("list_my_time", "Lists the signed-in user's own time entries between two dates, with each day's total. Use it for questions like 'what did I log this week?', and to get an entry's id before update_time or delete_time.")]
                ToolInvocationContext context,
            [McpToolProperty("from", "The first day, written yyyy-MM-dd.", isRequired: true)]
                string from,
            [McpToolProperty("to", "The last day, written yyyy-MM-dd.", isRequired: true)]
                string to)
        {
            string me = _securityService.GetCurrentTeamMemberId();
            if (me == null)
            {
                return NotIdentified;
            }

            if (!TryReadDate(from, out DateOnly? first) || first == null ||
                !TryReadDate(to, out DateOnly? last) || last == null)
            {
                return "Both dates must be written yyyy-MM-dd, e.g. 2026-10-12.";
            }
            if (last.Value < first.Value)
            {
                return "The last day can't be before the first.";
            }
            if (last.Value.DayNumber - first.Value.DayNumber >= MaxListDays)
            {
                return $"Ask for at most {MaxListDays} days at a time.";
            }

            WorkNameLookup names = WorkNames();
            List<TimeEntry> entries = MyEntries(me)
                .Where(e => e.Date >= first.Value && e.Date <= last.Value)
                .OrderBy(e => e.Date)
                .ToList();

            return new
            {
                totalHours = entries.Sum(e => e.Hours),
                days = entries
                    .GroupBy(e => e.Date)
                    .Select(day => new
                    {
                        date = day.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        weekday = day.Key.ToString("dddd", CultureInfo.InvariantCulture),
                        hours = day.Sum(e => e.Hours),
                        entries = day.Select(e => new
                        {
                            entryId = e.Id,
                            hours = e.Hours,
                            work = Describe(e, names),
                            notes = e.Notes,
                            // Billed on an issued invoice, so it can't be changed.
                            invoiced = e.Invoiced,
                        }),
                    }),
            };
        }

        // Changes one of the caller's own entries, through the Time Log's own
        // checks, in its order (TimeEntryFunctions.RunUpdateTimeEntry).
        [Allow(Role.Anyone)]
        [Function(nameof(McpUpdateTime))]
        public string McpUpdateTime(
            [McpToolTrigger("update_time", "Changes one of the signed-in user's own time entries: its task, date, hours or notes. Get the entry's id from list_my_time, and confirm the change with the user first. Leave out whatever stays the same.")]
                ToolInvocationContext context,
            [McpToolProperty("entryId", "The entry's id, from list_my_time.", isRequired: true)]
                string entryId,
            [McpToolProperty("subTaskId", "A different task's subTaskId, from find_my_work.")]
                string subTaskId,
            [McpToolProperty("date", "A different day, written yyyy-MM-dd.")]
                string date,
            [McpToolProperty("hours", "Different hours, in quarter-hour steps, e.g. 1.5.")]
                string hours,
            [McpToolProperty("notes", "New notes, replacing the old ones.")]
                string notes)
        {
            string me = _securityService.GetCurrentTeamMemberId();
            if (me == null)
            {
                return NotIdentified;
            }

            TimeEntry existing = MyEntries(me).FirstOrDefault(e => e.Id == entryId);
            if (existing == null)
            {
                return NoSuchEntry;
            }

            // The entry as it would be saved: what's stored, with the changes.
            // Its project and subproject are filled in again from the task.
            TimeEntry changed = new TimeEntry
            {
                Id = existing.Id,
                CustomerId = existing.CustomerId,
                TeamMemberId = me,
                ProjectSubTaskId = existing.ProjectSubTaskId,
                Date = existing.Date,
                Hours = existing.Hours,
                Notes = existing.Notes,
                Billable = existing.Billable,
            };

            if (!string.IsNullOrWhiteSpace(subTaskId))
            {
                // The id must be real: a made-up one would break the save.
                if (!_subTaskRepo.GetAll().Any(s => s.Id == subTaskId))
                {
                    return "There's no task with that id. Use find_my_work to look it up.";
                }
                changed.ProjectSubTaskId = subTaskId;
            }
            if (!string.IsNullOrWhiteSpace(date))
            {
                if (!TryReadDate(date, out DateOnly? day) || day == null)
                {
                    return "The date must be written yyyy-MM-dd, e.g. 2026-10-15.";
                }
                changed.Date = day.Value;
            }
            if (!string.IsNullOrWhiteSpace(hours))
            {
                if (!decimal.TryParse(hours.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount))
                {
                    return "Hours must be a number, e.g. 1.5.";
                }
                changed.Hours = amount;
            }
            if (!string.IsNullOrWhiteSpace(notes))
            {
                changed.Notes = notes.Trim();
            }

            if (!_timeEntryService.MayWrite(changed.Id))
            {
                return "That entry isn't yours, so you can't change it.";
            }
            string invalid = _timeEntryService.Validate(changed);
            if (invalid != null)
            {
                return invalid;
            }
            // Time billed on an issued invoice stays as it was billed.
            string locked = _timeEntryService.LockedReason(changed.Id, changed);
            if (locked != null)
            {
                return locked;
            }
            if (!_timeEntryService.MayLogAgainst(_timeEntryService.OwnerFor(changed), changed.ProjectSubTaskId))
            {
                return NotOnTeam;
            }

            TimeEntry saved = _timeEntryService.Update(changed);
            return $"Changed the entry to {HoursText(saved.Hours)}h on {Describe(saved, WorkNames())} for {DayText(saved.Date)}.";
        }

        // Deletes one of the caller's own entries, through the Time Log's own
        // checks (TimeEntryFunctions.RunDeleteTimeEntry).
        [Allow(Role.Anyone)]
        [Function(nameof(McpDeleteTime))]
        public string McpDeleteTime(
            [McpToolTrigger("delete_time", "Deletes one of the signed-in user's own time entries. Get its id from list_my_time, and confirm with the user before deleting: it can't be undone.")]
                ToolInvocationContext context,
            [McpToolProperty("entryId", "The entry's id, from list_my_time.", isRequired: true)]
                string entryId)
        {
            string me = _securityService.GetCurrentTeamMemberId();
            if (me == null)
            {
                return NotIdentified;
            }

            TimeEntry existing = MyEntries(me).FirstOrDefault(e => e.Id == entryId);
            if (existing == null)
            {
                return NoSuchEntry;
            }

            if (!_timeEntryService.MayWrite(existing.Id))
            {
                return "That entry isn't yours, so you can't delete it.";
            }
            // Time billed on an issued invoice stays as it was billed.
            string locked = _timeEntryService.LockedReason(existing.Id);
            if (locked != null)
            {
                return locked;
            }

            string what = $"{HoursText(existing.Hours)}h on {Describe(existing, WorkNames())} for {DayText(existing.Date)}";
            _timeEntryService.Delete(existing.Id);
            return $"Deleted {what}.";
        }

        // The caller's own time entries. GetAllTimeEntries also gives a project
        // manager their projects' entries and an admin everyone's, so it's
        // narrowed to the caller's.
        private List<TimeEntry> MyEntries(string me)
        {
            return _timeEntryService.GetAllTimeEntries().Where(e => e.TeamMemberId == me).ToList();
        }

        // Projects, subprojects and tasks by id, for naming where time went.
        private sealed class WorkNameLookup
        {
            public Dictionary<string, Project> Projects { get; init; }
            public Dictionary<string, ProjectTask> Subprojects { get; init; }
            public Dictionary<string, ProjectSubTask> Tasks { get; init; }
        }

        private WorkNameLookup WorkNames()
        {
            return new WorkNameLookup
            {
                Projects = _projectService.GetAllProjects().ToDictionary(p => p.Id),
                Subprojects = _taskRepo.GetAll().ToDictionary(t => t.Id),
                Tasks = _subTaskRepo.GetAll().ToDictionary(s => s.Id),
            };
        }

        // "Valley Line › Design › Drawings": where a time entry was logged.
        private static string Describe(TimeEntry entry, WorkNameLookup names)
        {
            string project = entry.ProjectId != null && names.Projects.TryGetValue(entry.ProjectId, out Project p) ? p.Name : "Unknown project";
            string subproject = entry.ProjectTaskId != null && names.Subprojects.TryGetValue(entry.ProjectTaskId, out ProjectTask s) ? s.Name : "?";
            string task = entry.ProjectSubTaskId != null && names.Tasks.TryGetValue(entry.ProjectSubTaskId, out ProjectSubTask t) ? t.Name : "?";
            return $"{project} › {subproject} › {task}";
        }

        // "1.5", "8": hours as Claude should repeat them.
        private static string HoursText(decimal hours)
        {
            return hours.ToString("0.##", CultureInfo.InvariantCulture);
        }

        // "Thu Oct 15, 2026".
        private static string DayText(DateOnly day)
        {
            return day.ToString("ddd MMM d, yyyy", CultureInfo.InvariantCulture);
        }

        // A date written yyyy-MM-dd, or nothing. False when it's something else.
        private static bool TryReadDate(string text, out DateOnly? date)
        {
            date = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }
            if (DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsed))
            {
                date = parsed;
                return true;
            }
            // The Functions binding turns "2026-11-01" into a date and passes it on as "11/01/2026 00:00:00 -06:00".
            if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset converted))
            {
                date = DateOnly.FromDateTime(converted.DateTime);
                return true;
            }
            return false;
        }



    }
}
