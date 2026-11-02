using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace sma.plan
{
	// Imports the last two months from Harvest - from the Monday of the week
	// holding the 1st of last month - into a test database, to see SMA's
	// real data in the app. Dev only, and only ever into a database whose name
	// ends in HARVEST, which it empties first - so it can be run again and
	// again. Not the final migration: nothing remembers which Harvest record a
	// record came from.
	//
	// A Harvest client is either one job or a company with several:
	//   Harvest client   [493] VLW Construction Phase  -> project, code 493
	//   Harvest project  0100 MST PM and Admin 2026/27 -> subproject
	//   Harvest task     0100-010 ...                  -> cost code
	//
	//   Harvest client   AECOM Canada ULC (no [job])   -> client
	//   its Harvest projects with code 453             -> project 453, each a subproject
	//
	//   "Z Paid Hours Off" projects                    -> the paid hours off cost codes
	// A job's client is left empty, to set by hand. Each person's rates, as
	// Harvest priced their time, go on rate lists - one per job, or shared by
	// jobs that billed the same rates. Invoices come in as Harvest had them -
	// one subproject, a job, or several of a company's jobs - issued; expenses
	// approved and reimbursed, without receipts.
	//
	// Given SMA's Access tables, those overrule Harvest. Its invoices: paid or
	// not and when, the month and date, the number and job, the total, and
	// each job's disbursement rate - invoices from before the earliest of them
	// count as paid. Its clients: their names, and the rest of them. Its jobs
	// (those it exports): client, division, PO, dates, project manager,
	// status, notes and budget.
	internal static class HarvestImport
	{
		public const string DatabaseSuffix = "HARVEST";

		public static bool Enabled() => DevSeed.Allowed(DatabaseSuffix);

		// The JSON file holding { "token": ..., "accountId": ... }. A local
		// setting only - never set in Azure.
		public static string TokenFile() => Environment.GetEnvironmentVariable("HARVEST_TOKEN_FILE");

		// Optional: the folder SMA's Access tables are exported to (see
		// AccessExport), which overrule Harvest on what they have. A local
		// setting only, like the token.
		public static string AccessExportFolder() => Environment.GetEnvironmentVariable("ACCESS_EXPORT_FOLDER");
	}

	// What an import made, and what didn't fit.
	internal class HarvestImportSummary
	{
		public string From { get; set; }
		public int Clients { get; set; }
		public int Projects { get; set; }
		public int Subprojects { get; set; }
		public int CostCodes { get; set; }
		public int People { get; set; }
		public int RateLists { get; set; }
		public int TimeEntries { get; set; }
		public int Invoices { get; set; }
		public int Expenses { get; set; }
		public double Seconds { get; set; }
		public int WarningCount { get; set; }
		public List<string> Warnings { get; set; }
	}

	internal class HarvestImporter
	{
		private const string Api = "https://api.harvestapp.com/v2";

		// Harvest's paid hours off projects, by code, then by name.
		private static readonly Dictionary<string, string> TimeOffByCode = new()
		{
			["A02-01"] = TimeOffTypes.Vacation,
			["A02-03"] = TimeOffTypes.PersonalLeave,
			["A02-04"] = TimeOffTypes.SickLeave,
			["A02-05"] = TimeOffTypes.LunchBreak,
			["A02-06"] = TimeOffTypes.UnpaidLeave,
			["A04-01"] = TimeOffTypes.StatHoliday,
		};
		// First match wins, so "unpaid" comes first: "Unpaid sick day" is unpaid.
		private static readonly (Regex Name, string Type)[] TimeOffByName =
		{
			(new Regex("unpaid", RegexOptions.IgnoreCase), TimeOffTypes.UnpaidLeave),
			(new Regex("lunch", RegexOptions.IgnoreCase), TimeOffTypes.LunchBreak),
			(new Regex("stat", RegexOptions.IgnoreCase), TimeOffTypes.StatHoliday),
			(new Regex("vacation", RegexOptions.IgnoreCase), TimeOffTypes.Vacation),
			(new Regex("personal", RegexOptions.IgnoreCase), TimeOffTypes.PersonalLeave),
			(new Regex("sick", RegexOptions.IgnoreCase), TimeOffTypes.SickLeave),
		};

		// Harvest expense category -> the app's (the frontend's model/Expense.js).
		private static readonly (Regex Name, string Category)[] Categories =
		{
			(new Regex("parking", RegexOptions.IgnoreCase), "Parking"),
			(new Regex("meal|entertain", RegexOptions.IgnoreCase), "Meals"),
			(new Regex("mileage|kilomet", RegexOptions.IgnoreCase), "Mileage"),
			(new Regex("supplies", RegexOptions.IgnoreCase), "Supplies"),
			(new Regex("airfare|flight", RegexOptions.IgnoreCase), "Airfare"),
			(new Regex("hotel|accommodation|lodging", RegexOptions.IgnoreCase), "Accommodation"),
			(new Regex("taxi|transit|uber", RegexOptions.IgnoreCase), "Taxi / Transit"),
			(new Regex("print", RegexOptions.IgnoreCase), "Printing"),
			(new Regex("equipment", RegexOptions.IgnoreCase), "Equipment"),
		};

		private readonly HttpClient _http;
		private readonly string _callerEmail;
		private readonly Dictionary<string, ProjectSubTask> _timeOff;
		// Access's invoices by number, clients and jobs; empty without them.
		private readonly Dictionary<int, AccessInvoice> _access;
		private readonly List<AccessClient> _accessClients;
		private readonly List<AccessProject> _accessProjects;
		// Named groups of jobs to share a rate list (rate-list-groups.txt).
		private readonly List<(string Name, List<string> Jobs)> _rateListGroups;
		// Jobs share a rate list when the same people's rates were compared in
		// at least this many months - and always agreed.
		private const int SharedMonthsForOneList = 10;
		// The jobs whose disbursement rate Access's job table set, which its
		// invoices don't overrule.
		private readonly HashSet<string> _rateFromAccessJob = new();
		private readonly DateOnly _today = DateOnly.FromDateTime(DateTime.Today);
		private readonly DateOnly _from;
		private readonly List<string> _warnings = new();

		private readonly List<User> _users = new();
		private readonly List<TeamMember> _people = new();
		private readonly List<RateList> _rateLists = new();
		private readonly List<Customer> _customers = new();
		private readonly List<Project> _projects = new();
		private readonly List<ProjectTask> _subprojects = new();
		private readonly List<ProjectSubTask> _costCodes = new();
		private readonly List<ProjectAssignment> _assignments = new();
		private readonly List<ProjectBillingRate> _rates = new();
		private readonly List<ProjectFinancials> _financials = new();
		private readonly List<TimeEntry> _entries = new();
		private readonly List<Expense> _expenses = new();
		private readonly List<Invoice> _invoices = new();

		// Harvest ids -> what they became.
		private readonly Dictionary<long, TeamMember> _personOf = new();
		// A job client's one project; a company's, one per job.
		private readonly Dictionary<long, List<Project>> _projectsOfClient = new();
		private readonly Dictionary<long, Project> _projectOf = new();
		private readonly Dictionary<long, ProjectTask> _subprojectOf = new();
		private readonly Dictionary<(long Project, long Task), ProjectSubTask> _costCodeOf = new();
		private readonly Dictionary<long, ProjectSubTask> _timeOffOf = new();
		private User _admin;

		public HarvestImporter(string tokenFile, string callerEmail, Dictionary<string, ProjectSubTask> timeOff,
			AccessExport access = null)
		{
			_access = access?.Invoices ?? new Dictionary<int, AccessInvoice>();
			_accessClients = access?.Clients ?? new List<AccessClient>();
			_accessProjects = access?.Projects ?? new List<AccessProject>();
			_rateListGroups = access?.RateListGroups ?? new List<(string Name, List<string> Jobs)>();
			if (access?.Files.Count > 0)
			{
				Warn($"Access: read {string.Join(", ", access.Files)}");
			}
			using JsonDocument cfg = JsonDocument.Parse(File.ReadAllText(tokenFile));
			_http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
			_http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cfg.RootElement.GetProperty("token").GetString());
			_http.DefaultRequestHeaders.Add("Harvest-Account-Id", cfg.RootElement.GetProperty("accountId").ToString());
			_http.DefaultRequestHeaders.UserAgent.ParseAdd("SMA-timesheets-import/1.0");
			_callerEmail = (callerEmail ?? "").Trim().ToLowerInvariant();
			_timeOff = timeOff;
			// Two months, the current one so far - from the Monday of the week
			// holding the 1st of last month, as Staff hours shows whole weeks.
			DateOnly firstOfLast = new DateOnly(_today.Year, _today.Month, 1).AddMonths(-1);
			_from = firstOfLast.AddDays(-(((int)firstOfLast.DayOfWeek + 6) % 7));
		}

		public HarvestImportSummary Run()
		{
			var clock = Stopwatch.StartNew();
			string from = _from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

			List<JsonElement> users = All("/users", "users");
			List<JsonElement> harvestRoles = All("/roles", "roles");
			List<JsonElement> clients = All("/clients", "clients");
			List<JsonElement> projects = All("/projects", "projects");
			List<JsonElement> userAssignments = All("/user_assignments", "user_assignments");
			List<JsonElement> taskAssignments = All("/task_assignments", "task_assignments");
			List<JsonElement> categories = All("/expense_categories", "expense_categories");
			List<JsonElement> entries = All($"/time_entries?from={from}", "time_entries");
			List<JsonElement> invoices = All($"/invoices?from={from}", "invoices");
			List<JsonElement> expenses = All($"/expenses?from={from}", "expenses");

			MakePeople(users, harvestRoles, entries);
			MakeProjects(clients, projects, userAssignments, taskAssignments, entries, expenses);
			// Before the rates, whose lists are named by client, and the
			// invoices, billed to Access's clients.
			FollowAccessJobs();
			MakeTime(entries);
			MakeTeamsAndRates(entries, userAssignments);
			MakeExpenses(expenses, categories);
			MakeInvoices(invoices, entries, expenses);
			SetDisbursementRates();

			DevSeedGenerator.Insert(_users);
			DevSeedGenerator.Insert(_people);
			DevSeedGenerator.Insert(_rateLists);
			DevSeedGenerator.Insert(_customers);
			DevSeedGenerator.Insert(_projects);
			DevSeedGenerator.Insert(_subprojects);
			DevSeedGenerator.Insert(_costCodes);
			DevSeedGenerator.Insert(_assignments);
			DevSeedGenerator.Insert(_rates);
			DevSeedGenerator.Insert(_financials);
			DevSeedGenerator.Insert(_entries);
			DevSeedGenerator.Insert(_expenses);
			DevSeedGenerator.Insert(_invoices);

			return new HarvestImportSummary
			{
				From = from,
				Clients = _customers.Count,
				Projects = _projects.Count,
				Subprojects = _subprojects.Count,
				CostCodes = _costCodes.Count,
				People = _people.Count,
				RateLists = _rateLists.Count,
				TimeEntries = _entries.Count,
				Invoices = _invoices.Count,
				Expenses = _expenses.Count,
				Seconds = Math.Round(clock.Elapsed.TotalSeconds, 1),
				WarningCount = _warnings.Count,
				Warnings = _warnings.Take(200).ToList(),
			};
		}

		// ---- People ---------------------------------------------------------

		// Everyone active, or with time in the window. Only active people get a
		// sign-in: staff hours checks everyone who has one, and those who've
		// left would show as missing time. The caller - and Harvest's
		// administrators - are admins.
		private void MakePeople(List<JsonElement> users, List<JsonElement> harvestRoles, List<JsonElement> entries)
		{
			HashSet<long> withTime = entries.Select(e => IdOf(e, "user")).ToHashSet();
			var rolesOf = new Dictionary<long, List<string>>();
			foreach (JsonElement role in harvestRoles)
			{
				foreach (JsonElement id in role.GetProperty("user_ids").EnumerateArray())
				{
					long userId = id.GetInt64();
					if (!rolesOf.ContainsKey(userId)) rolesOf[userId] = new List<string>();
					rolesOf[userId].Add(Str(role, "name") ?? "");
				}
			}

			foreach (JsonElement u in users.Where(u => Bool(u, "is_active") || withTime.Contains(Long(u, "id"))))
			{
				long harvestId = Long(u, "id");
				bool active = Bool(u, "is_active");
				string name = $"{Str(u, "first_name")} {Str(u, "last_name")}".Trim();
				string email = (Str(u, "email") ?? "").Trim().ToLowerInvariant();
				List<string> roles = rolesOf.TryGetValue(harvestId, out var r) ? r : new List<string>();
				string employeeId = roles.FirstOrDefault(x => Regex.IsMatch(x, @"^\s*[\d#-]+\s*$"))?.Trim();
				string division = roles.FirstOrDefault(x => x.StartsWith("Division", StringComparison.OrdinalIgnoreCase));

				var person = new TeamMember
				{
					Id = NewId(),
					Name = name,
					Description = string.Join(" · ", new[]
					{
						employeeId != null ? $"Employee ID {employeeId}" : null,
						division,
						active ? null : "Inactive in Harvest",
					}.Where(x => x != null)),
					PercentAvailable = active ? 100 : 0,
				};

				if (active && email.Length > 0)
				{
					var user = new User { Id = NewId(), Email = email, DisplayName = name };
					bool harvestAdmin = u.TryGetProperty("access_roles", out JsonElement access)
						&& access.EnumerateArray().Any(a => a.GetString() == "administrator");
					if (harvestAdmin || email == _callerEmail)
					{
						user.Privileges.Add(User.AdminPrivilege);
					}
					person.UserId = user.Id;
					_users.Add(user);
					if (email == _callerEmail)
					{
						_admin = user;
					}
				}
				_people.Add(person);
				_personOf[harvestId] = person;
			}

			// Someone not in Harvest still gets in, to look around.
			if (_admin == null)
			{
				_admin = new User { Id = NewId(), Email = _callerEmail, DisplayName = _callerEmail };
				_admin.Privileges.Add(User.AdminPrivilege);
				_users.Add(_admin);
				_people.Add(new TeamMember { Id = NewId(), Name = _callerEmail, UserId = _admin.Id, PercentAvailable = 100 });
			}
		}

		// ---- Projects, subprojects and cost codes ---------------------------

		// The Harvest projects with time or expenses in the window, or still
		// active - grouped by their Harvest client into projects.
		private void MakeProjects(List<JsonElement> clients, List<JsonElement> projects, List<JsonElement> userAssignments,
			List<JsonElement> taskAssignments, List<JsonElement> entries, List<JsonElement> expenses)
		{
			HashSet<long> paidTimeOffClients = clients
				.Where(c => Regex.IsMatch(Str(c, "name") ?? "", "paid hours off", RegexOptions.IgnoreCase))
				.Select(c => Long(c, "id"))
				.ToHashSet();
			HashSet<long> used = entries.Select(e => IdOf(e, "project")).Concat(expenses.Select(e => IdOf(e, "project"))).ToHashSet();
			List<JsonElement> included = projects.Where(p => Bool(p, "is_active") || used.Contains(Long(p, "id"))).ToList();
			var clientById = clients.ToDictionary(c => Long(c, "id"));

			foreach (JsonElement p in included.Where(p => paidTimeOffClients.Contains(IdOf(p, "client"))))
			{
				string code = Str(p, "code")?.Trim();
				string type = code != null && TimeOffByCode.TryGetValue(code, out string byCode)
					? byCode
					: TimeOffByName.FirstOrDefault(t => t.Name.IsMatch(Str(p, "name") ?? "")).Type;
				if (type != null && _timeOff.TryGetValue(type, out ProjectSubTask task))
				{
					_timeOffOf[Long(p, "id")] = task;
				}
				else
				{
					Warn($"Paid hours off: \"{Str(p, "name")}\" ({code}) matches none of the app's - its time is left out");
				}
			}

			var managers = userAssignments
				.Where(a => Bool(a, "is_project_manager"))
				.GroupBy(a => IdOf(a, "project"))
				.ToDictionary(g => g.Key, g => g.Select(a => IdOf(a, "user")).ToList());

			foreach (var client in included.Where(p => !paidTimeOffClients.Contains(IdOf(p, "client"))).GroupBy(p => IdOf(p, "client")))
			{
				List<JsonElement> harvestProjects = client.ToList();
				bool known = clientById.TryGetValue(client.Key, out JsonElement harvestClient);
				string clientName = known
					? Str(harvestClient, "name")
					: Str(harvestProjects[0].GetProperty("client"), "name");
				var made = new List<Project>();

				if (IsCompany(clientName))
				{
					// A company: it's the client, and each of its jobs - its Harvest
					// projects sharing a code - a project of its own.
					string currency = known ? Str(harvestClient, "currency")?.Trim().ToUpperInvariant() : null;
					var customer = new Customer
					{
						Id = NewId(),
						Name = Collapse(clientName),
						Currency = currency != null && currency.Length == 3 ? currency : "CAD",
					};
					_customers.Add(customer);
					foreach (var job in harvestProjects.GroupBy(p => Str(p, "code")?.Trim() is string jc && jc.Length > 0 ? jc : $"#{Long(p, "id")}"))
					{
						// Named after the job's first Harvest project: later ones are
						// its claims and extras.
						List<JsonElement> parts = job.OrderBy(p => Long(p, "id")).ToList();
						string jobCode = job.Key.StartsWith('#') ? null : job.Key;
						made.Add(MakeProject(SubprojectName(Str(parts[0], "name")), jobCode, clientName, parts, customer, managers));
					}
				}
				else
				{
					List<string> codes = harvestProjects.Select(p => Str(p, "code")?.Trim()).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
					string code = JobCode(clientName) ?? (codes.Count == 1 ? codes[0] : CommonPrefix(codes));
					made.Add(MakeProject(JobName(clientName), code, clientName, harvestProjects, null, managers));
				}
				_projectsOfClient[client.Key] = made;
			}

			// Task assignments -> cost codes: those used in the window, or still
			// active. Billable as Harvest has them, unless the project isn't.
			HashSet<(long, long)> usedTasks = entries.Select(e => (IdOf(e, "project"), IdOf(e, "task"))).ToHashSet();
			var byKey = new Dictionary<(string, string), ProjectSubTask>();
			foreach (JsonElement ta in taskAssignments)
			{
				long hp = IdOf(ta, "project");
				long task = IdOf(ta, "task");
				if (!_subprojectOf.TryGetValue(hp, out ProjectTask sub) || (!Bool(ta, "is_active") && !usedTasks.Contains((hp, task))))
				{
					continue;
				}
				_costCodeOf[(hp, task)] = CostCode(sub, Str(ta.GetProperty("task"), "name"), Bool(ta, "billable") && _projectOf[hp].Billable, byKey);
			}
			_costCodeByName = byKey;
		}

		// A Harvest client that's a company with jobs under it, not a job itself:
		// no "[job number]" in its name, and not one of SMA's own (Z ...).
		private static bool IsCompany(string clientName) =>
			JobCode(clientName) == null && !Regex.IsMatch(clientName ?? "", @"^\s*z\s", RegexOptions.IgnoreCase);

		// One job: a project - billed as its Harvest projects are, Lump sum when
		// they're all fixed fee - with those Harvest projects as its subprojects.
		// Of a company's when `customer` is set; otherwise its client is left to
		// set by hand.
		private Project MakeProject(string name, string code, string clientName, List<JsonElement> harvestProjects,
			Customer customer, Dictionary<long, List<long>> managers)
		{
			bool active = harvestProjects.Any(p => Bool(p, "is_active"));
			bool billable = harvestProjects.Any(p => Bool(p, "is_billable"));
			int fixedFee = harvestProjects.Count(p => Bool(p, "is_fixed_fee"));

			List<string> pos = harvestProjects
				.OrderByDescending(p => Str(p, "updated_at"))
				.Select(p => PoOf(Str(p, "name")))
				.Where(x => x != null)
				.ToList();
			if (pos.Distinct().Count() > 1)
			{
				Warn($"{name}: subprojects name {pos.Distinct().Count()} POs - kept {pos[0]}");
			}
			if (fixedFee > 0 && fixedFee < harvestProjects.Count)
			{
				Warn($"{name}: {fixedFee} of {harvestProjects.Count} subprojects are fixed fee - imported as hourly");
			}

			List<long> pms = harvestProjects
				.SelectMany(p => managers.TryGetValue(Long(p, "id"), out var list) ? list : new List<long>())
				.GroupBy(x => x)
				.OrderByDescending(g => g.Count())
				.Select(g => g.Key)
				.Where(_personOf.ContainsKey)
				.ToList();
			if (pms.Count > 1)
			{
				Warn($"{name}: {pms.Count} project managers in Harvest - kept {_personOf[pms[0]].Name}");
			}

			List<DateOnly> starts = harvestProjects.Select(p => Str(p, "starts_on")).Where(x => x != null).Select(Day).ToList();
			List<DateOnly> ends = harvestProjects.Select(p => Str(p, "ends_on")).Where(x => x != null).Select(Day).ToList();
			bool isInternal = Regex.IsMatch(clientName, @"^\s*z\s", RegexOptions.IgnoreCase);

			var project = new Project
			{
				Id = NewId(),
				Name = name,
				FundingCode = code,
				PoNumber = pos.FirstOrDefault(),
				ProjectType = !billable ? Project.TypeNonBillable : fixedFee == harvestProjects.Count ? Project.TypeLumpSum : Project.TypeHourly,
				Billable = billable,
				Status = isInternal ? Project.StatusSma : active ? Project.StatusInProcess : Project.StatusFinished,
				ProjectManagerId = pms.Count > 0 ? _personOf[pms[0]].Id : null,
				StartDate = starts.Count > 0 ? starts.Min() : (DateOnly?)null,
				EndDate = !active && ends.Count > 0 ? ends.Max() : (DateOnly?)null,
				CustomerId = customer?.Id,
				Notes = customer != null && code != null
					? $"Imported from Harvest: {clientName}, job {code}"
					: $"Imported from Harvest: {clientName}",
			};
			_projects.Add(project);

			// Harvest's project budgets, added up, as a starting total: a fixed
			// fee project's fee (its cost budget is the fee again), otherwise its
			// cost budget.
			decimal budget = harvestProjects.Sum(p => Bool(p, "is_fixed_fee") ? Dec(p, "fee") : Dec(p, "cost_budget"));
			if (budget > 0 && billable)
			{
				_financials.Add(new ProjectFinancials
				{
					Id = NewId(),
					ProjectId = project.Id,
					ContractValue = budget,
					TotalBudget = budget,
					UpdatedAt = DateTime.UtcNow,
					UpdatedBy = _admin.Id,
				});
			}

			// Harvest projects -> subprojects. One renamed each year (its
			// year and PO in the name) stays one; two whose names differ only
			// by those are merged.
			var byName = new Dictionary<string, ProjectTask>();
			foreach (JsonElement hp in harvestProjects)
			{
				string subName = SubprojectName(Str(hp, "name"));
				if (!byName.TryGetValue(subName, out ProjectTask sub))
				{
					string ownCode = Str(hp, "code")?.Trim();
					sub = new ProjectTask
					{
						Id = NewId(),
						ProjectId = project.Id,
						Name = subName,
						ShortName = !string.IsNullOrEmpty(ownCode) && ownCode != code ? ownCode : null,
						Active = false,
					};
					byName[subName] = sub;
					_subprojects.Add(sub);
				}
				else
				{
					Warn($"{name}: \"{Str(hp, "name")}\" merged into subproject \"{subName}\"");
				}
				sub.Active |= Bool(hp, "is_active");
				_subprojectOf[Long(hp, "id")] = sub;
				_projectOf[Long(hp, "id")] = project;
			}

			return project;
		}

		private Dictionary<(string, string), ProjectSubTask> _costCodeByName = new();

		private ProjectSubTask CostCode(ProjectTask sub, string name, bool billable, Dictionary<(string, string), ProjectSubTask> byKey)
		{
			name = (name ?? "").Trim();
			if (byKey.TryGetValue((sub.Id, name), out ProjectSubTask existing))
			{
				existing.Billable |= billable;
				return existing;
			}
			var costCode = new ProjectSubTask
			{
				Id = NewId(),
				ProjectId = sub.ProjectId,
				ProjectTaskId = sub.Id,
				Name = name,
				Billable = billable,
			};
			byKey[(sub.Id, name)] = costCode;
			_costCodes.Add(costCode);
			return costCode;
		}

		// ---- Time -------------------------------------------------------------

		// Harvest invoice id -> the entries it billed, to link once invoices exist.
		private readonly Dictionary<long, List<TimeEntry>> _entriesBilledOn = new();

		private void MakeTime(List<JsonElement> entries)
		{
			int notQuarters = 0;
			int lockedByHand = 0;
			foreach (JsonElement e in entries)
			{
				long hp = IdOf(e, "project");
				if (!_personOf.TryGetValue(IdOf(e, "user"), out TeamMember person))
				{
					Warn($"Time entry {Long(e, "id")}: its person wasn't imported");
					continue;
				}

				ProjectSubTask costCode;
				if (_timeOffOf.TryGetValue(hp, out ProjectSubTask timeOff))
				{
					costCode = timeOff;
				}
				else if (_subprojectOf.TryGetValue(hp, out ProjectTask sub))
				{
					long task = IdOf(e, "task");
					if (!_costCodeOf.TryGetValue((hp, task), out costCode))
					{
						costCode = CostCode(sub, Str(e.GetProperty("task"), "name"), Bool(e, "billable"), _costCodeByName);
						_costCodeOf[(hp, task)] = costCode;
					}
				}
				else
				{
					continue; // a paid hours off project that matched nothing, already warned about
				}

				decimal hours = Dec(e, "hours");
				if (hours * 4 != Math.Floor(hours * 4))
				{
					notQuarters++;
				}
				var entry = new TimeEntry
				{
					Id = NewId(),
					ProjectId = costCode.ProjectId,
					ProjectTaskId = costCode.ProjectTaskId,
					ProjectSubTaskId = costCode.Id,
					TeamMemberId = person.Id,
					Date = Day(Str(e, "spent_date")),
					Hours = hours,
					Notes = string.IsNullOrWhiteSpace(Str(e, "notes")) ? null : Str(e, "notes").Trim(),
					Billable = Bool(e, "billable"),
					Invoiced = Bool(e, "is_billed"),
					LastModified = When(Str(e, "updated_at")),
					ModifiedBy = person.Id,
				};
				_entries.Add(entry);
				_harvestEntryOf[entry] = e;

				if (e.TryGetProperty("invoice", out JsonElement inv) && inv.ValueKind == JsonValueKind.Object)
				{
					long invoiceId = Long(inv, "id");
					if (!_entriesBilledOn.ContainsKey(invoiceId)) _entriesBilledOn[invoiceId] = new List<TimeEntry>();
					_entriesBilledOn[invoiceId].Add(entry);
				}
				else if (entry.Invoiced)
				{
					// Marked billed in Harvest by hand - billed outside it - so
					// locked by hand here, as no invoice of ours will lock it.
					entry.LockedAt = entry.LastModified;
					entry.LockedBy = _admin.Id;
					lockedByHand++;
				}
			}
			if (lockedByHand > 0)
			{
				Warn($"Time: {lockedByHand} entries billed outside Harvest are locked");
			}
			if (notQuarters > 0)
			{
				Warn($"Time: {notQuarters} entries aren't in quarter hours (kept as Harvest has them)");
			}
		}

		private readonly Dictionary<TimeEntry, JsonElement> _harvestEntryOf = new();

		// ---- Teams and rates ------------------------------------------------------

		// Each person's rates, from what Harvest priced their time at, each from
		// the day it was first billed. A change counts once the new rate sticks:
		// someone flipping between two rates - logging to last year's and this
		// year's copy of a subproject - gets the later one from where it settles.
		// The rates go on rate lists, shared by jobs that billed the same rates
		// (below); every other job has its own. Everyone with time on a job, or
		// still assigned to it in Harvest, is on its team.
		private void MakeTeamsAndRates(List<JsonElement> harvestEntries, List<JsonElement> userAssignments)
		{
			var entriesByProject = _entries.GroupBy(e => e.ProjectId).ToDictionary(g => g.Key, g => g.ToList());

			// What each job billed: its billable hours at Harvest's rate, and each
			// person's rate in each month - the one most of their hours were at.
			var billedOn = _projects.ToDictionary(
				p => p,
				p => (entriesByProject.TryGetValue(p.Id, out List<TimeEntry> on) ? on : new List<TimeEntry>())
					.Where(e => e.Billable)
					.Select(e => (Entry: e, Rate: DecOrNull(_harvestEntryOf[e], "billable_rate") ?? 0))
					.Where(x => x.Rate > 0)
					.ToList());
			var monthly = billedOn.ToDictionary(
				kv => kv.Key,
				kv => kv.Value
					.GroupBy(x => (x.Entry.TeamMemberId, x.Entry.Date.Year, x.Entry.Date.Month))
					.ToDictionary(g => g.Key, g => g.GroupBy(x => x.Rate).OrderByDescending(r => r.Sum(x => x.Entry.Hours)).First().Key));

			// Two jobs' person-months in common: how many at the same rate, how
			// many not. Then the same for two groups of jobs.
			var compared = new Dictionary<(Project, Project), (int Same, int Differ)>();
			(int Same, int Differ) Compare(Project a, Project b)
			{
				if (compared.TryGetValue((a, b), out var known))
				{
					return known;
				}
				int same = 0, differ = 0;
				foreach (var (month, rate) in monthly[a])
				{
					if (monthly[b].TryGetValue(month, out decimal other))
					{
						if (other == rate) same++; else differ++;
					}
				}
				return compared[(a, b)] = compared[(b, a)] = (same, differ);
			}
			(int Same, int Differ) Between(List<Project> a, List<Project> b)
			{
				int same = 0, differ = 0;
				foreach (Project x in a)
				{
					foreach (Project y in b)
					{
						var (s, d) = Compare(x, y);
						same += s;
						differ += d;
					}
				}
				return (same, differ);
			}

			// The lists. Jobs share one when they billed the same rates: never one
			// person at two rates in the same month, and enough months in common
			// to go on. rate-list-groups.txt names groups to start from, less any
			// job billed differently from the rest; other jobs join them, or each
			// other, by the same test - the closest two first, while any pass.
			// Two named groups stay apart. Every other job with rates has its own.
			var groups = new List<(string Name, List<Project> Jobs)>();
			var grouped = new HashSet<Project>();
			foreach (var (name, codes) in _rateListGroups)
			{
				List<Project> jobs = _projects.Where(p => codes.Contains(p.FundingCode) && !grouped.Contains(p)).ToList();
				List<string> missing = codes.Where(c => !_projects.Any(p => p.FundingCode == c)).ToList();
				if (missing.Count > 0)
				{
					Warn($"Rates: jobs {string.Join(", ", missing)} in {AccessExport.RateListGroupsFile} weren't imported");
				}
				while (jobs.Count > 1)
				{
					var worst = jobs
						.Select(j => (Job: j, Differ: jobs.Where(k => k != j).Sum(k => Compare(j, k).Differ)))
						.OrderByDescending(x => x.Differ)
						.First();
					if (worst.Differ == 0)
					{
						break;
					}
					Warn($"Rates: {worst.Job.FundingCode} left out of {name}: {worst.Differ} times it billed someone a different rate than the others did that month");
					jobs.Remove(worst.Job);
				}
				if (jobs.Count > 0)
				{
					groups.Add((name, jobs));
					grouped.UnionWith(jobs);
				}
			}
			groups.AddRange(_projects
				.Where(p => !grouped.Contains(p) && monthly[p].Count > 0)
				.Select(p => ((string)null, new List<Project> { p })));

			string Codes(List<Project> jobs) => string.Join(", ", jobs.Select(j => j.FundingCode));
			while (true)
			{
				(int Same, int A, int B)? best = null;
				for (int a = 0; a < groups.Count; a++)
				{
					for (int b = a + 1; b < groups.Count; b++)
					{
						if (groups[a].Name != null && groups[b].Name != null)
						{
							continue;
						}
						var (same, differ) = Between(groups[a].Jobs, groups[b].Jobs);
						if (differ == 0 && same >= SharedMonthsForOneList && (best == null || same > best.Value.Same))
						{
							best = (same, a, b);
						}
					}
				}
				if (best == null)
				{
					break;
				}
				var (inCommon, keep, join) = best.Value;
				Warn($"Rates: {Codes(groups[join].Jobs)} share a list with {groups[keep].Name ?? Codes(groups[keep].Jobs)}: the same rates in all {inCommon} person-months compared");
				groups[keep] = (groups[keep].Name ?? groups[join].Name, groups[keep].Jobs.Concat(groups[join].Jobs).ToList());
				groups.RemoveAt(join);
			}

			// Each person's history on a list, from all its jobs' hours together.
			int settled = 0;
			var lists = new List<(RateList List, Dictionary<string, List<(decimal Rate, DateOnly From)>> People, List<Project> Jobs)>();
			foreach (var (name, jobs) in groups)
			{
				var people = new Dictionary<string, List<(decimal Rate, DateOnly From)>>();
				foreach (var theirs in jobs.SelectMany(j => billedOn[j]).GroupBy(x => x.Entry.TeamMemberId))
				{
					var steps = new List<(decimal Rate, DateOnly From)>();
					bool flipped = false;
					foreach (var x in theirs.OrderBy(x => x.Entry.Date))
					{
						if (steps.Count > 0 && steps[^1].Rate == x.Rate)
						{
							continue;
						}
						// Back to an earlier rate: the ones since didn't stick.
						int back = steps.FindIndex(s => s.Rate == x.Rate);
						if (back >= 0)
						{
							steps.RemoveRange(back + 1, steps.Count - back - 1);
							flipped = true;
							continue;
						}
						steps.Add((x.Rate, x.Entry.Date));
					}
					if (flipped) settled++;
					people[theirs.Key] = steps;
				}
				lists.Add((new RateList { Id = NewId(), Name = name }, people, jobs));
			}

			Dictionary<string, Customer> clientOf = _customers.ToDictionary(c => c.Id);
			foreach (var (list, people, jobs) in lists)
			{
				jobs.Sort((a, b) => string.CompareOrdinal(a.FundingCode, b.FundingCode));
				List<string> clients = jobs
					.Select(j => j.CustomerId != null && clientOf.TryGetValue(j.CustomerId, out Customer c) ? c.Name : null)
					.Distinct()
					.ToList();
				string client = clients.Count == 1 && clients[0] != null ? clients[0] : "Shared";
				if (string.IsNullOrWhiteSpace(list.Name))
				{
					list.Name = jobs.Count == 1
						? $"{jobs[0].FundingCode} {jobs[0].Name}".Trim()
						: $"{client} ({string.Join(", ", jobs.Select(j => j.FundingCode))})";
				}
				_rateLists.Add(list);
				foreach (Project job in jobs)
				{
					job.RateListId = list.Id;
				}

				// Each first rate from the earliest of its jobs' starts, so no hour
				// before the first billed day goes unpriced.
				DateOnly listStart = jobs
					.Select(j => j.StartDate ?? (entriesByProject.TryGetValue(j.Id, out var on) ? on.Min(e => e.Date) : _from))
					.Min();
				foreach (var (person, steps) in people)
				{
					for (int s = 0; s < steps.Count; s++)
					{
						_rates.Add(new ProjectBillingRate
						{
							Id = NewId(),
							RateListId = list.Id,
							TeamMemberId = person,
							HourlyRate = steps[s].Rate,
							StartDate = s == 0 && listStart < steps[0].From ? listStart : steps[s].From,
							EndDate = s + 1 < steps.Count ? steps[s + 1].From.AddDays(-1) : null,
						});
					}
				}
			}
			var shared = lists.Where(l => l.Jobs.Count > 1).ToList();
			Warn($"Rates: {lists.Count} rate lists, {shared.Count} shared by several jobs"
				+ (shared.Count > 0 ? ": " + string.Join("; ", shared.Select(l => string.Join(", ", l.Jobs.Select(j => j.FundingCode)))) : ""));
			if (settled > 0)
			{
				Warn($"Rates: {settled} people's rates went back and forth on a job; each one's later rate counts from where it settled");
			}

			// Who's on each project: everyone with time on it in the window, and
			// everyone Harvest still has assigned to one of its subprojects.
			var assigned = new Dictionary<Project, HashSet<TeamMember>>();
			foreach (TimeEntry e in _entries)
			{
				Project project = _projects.FirstOrDefault(p => p.Id == e.ProjectId);
				if (project == null) continue;
				if (!assigned.ContainsKey(project)) assigned[project] = new HashSet<TeamMember>();
				assigned[project].Add(_people.First(p => p.Id == e.TeamMemberId));
			}
			foreach (JsonElement a in userAssignments.Where(a => Bool(a, "is_active")))
			{
				if (_projectOf.TryGetValue(IdOf(a, "project"), out Project project) && _personOf.TryGetValue(IdOf(a, "user"), out TeamMember person))
				{
					if (!assigned.ContainsKey(project)) assigned[project] = new HashSet<TeamMember>();
					assigned[project].Add(person);
				}
			}
			var firstDay = _entries.GroupBy(e => (e.ProjectId, e.TeamMemberId)).ToDictionary(g => g.Key, g => g.Min(e => e.Date));

			foreach (var (project, people) in assigned)
			{
				DateOnly projectStart = project.StartDate ?? (entriesByProject.TryGetValue(project.Id, out var on) ? on.Min(e => e.Date) : _from);
				foreach (TeamMember person in people)
				{
					_assignments.Add(new ProjectAssignment
					{
						Id = NewId(),
						ProjectId = project.Id,
						TeamMemberId = person.Id,
						StartDate = firstDay.TryGetValue((project.Id, person.Id), out DateOnly day) ? day : projectStart,
					});
				}
			}
		}

		// The rates each project's hours are priced by - its list's - tagged with
		// the project, as an issued invoice keeps them.
		private List<ProjectBillingRate> RatesFor(IEnumerable<string> projectIds) =>
			_projects
				.Where(p => p.RateListId != null && projectIds.Contains(p.Id))
				.SelectMany(p => _rates
					.Where(r => r.RateListId == p.RateListId)
					.Select(r => new ProjectBillingRate
					{
						Id = r.Id,
						RateListId = r.RateListId,
						TeamMemberId = r.TeamMemberId,
						ProjectId = p.Id,
						HourlyRate = r.HourlyRate,
						StartDate = r.StartDate,
						EndDate = r.EndDate,
					}))
				.ToList();

		// ---- Expenses -----------------------------------------------------------

		// Harvest invoice id -> the expenses it billed.
		private readonly Dictionary<long, List<Expense>> _expensesBilledOn = new();

		// Approved and reimbursed - they were paid back long ago - so none waits
		// for review or a batch. No receipt files.
		private void MakeExpenses(List<JsonElement> expenses, List<JsonElement> categories)
		{
			var categoryOf = categories.ToDictionary(
				c => Long(c, "id"),
				c => Categories.FirstOrDefault(x => x.Name.IsMatch(Str(c, "name") ?? "")).Category ?? "Other");

			foreach (JsonElement x in expenses)
			{
				long hp = IdOf(x, "project");
				if (!_personOf.TryGetValue(IdOf(x, "user"), out TeamMember person) || !_subprojectOf.TryGetValue(hp, out ProjectTask sub))
				{
					Warn($"Expense {Long(x, "id")} on \"{Str(x.GetProperty("project"), "name")}\": its person or project wasn't imported");
					continue;
				}
				DateTime created = When(Str(x, "created_at"));
				var expense = new Expense
				{
					Id = NewId(),
					TeamMemberId = person.Id,
					ProjectId = sub.ProjectId,
					ProjectTaskId = sub.Id,
					Date = Day(Str(x, "spent_date")),
					Category = categoryOf.TryGetValue(IdOf(x, "expense_category"), out string category) ? category : "Other",
					Notes = Str(x, "notes")?.Trim(),
					Amount = Math.Round(Dec(x, "total_cost"), 2),
					Currency = "CAD",
					Billable = Bool(x, "billable"),
					Reimbursable = true,
					Status = Expense.StatusApproved,
					SubmittedAt = created,
					ReviewedBy = _admin.Id,
					ReviewedAt = created,
					Invoiced = Bool(x, "is_billed"),
					Reimbursed = true,
					ReimbursedAt = When(Str(x, "updated_at")),
				};
				_expenses.Add(expense);

				if (x.TryGetProperty("invoice", out JsonElement inv) && inv.ValueKind == JsonValueKind.Object)
				{
					long invoiceId = Long(inv, "id");
					if (!_expensesBilledOn.ContainsKey(invoiceId)) _expensesBilledOn[invoiceId] = new List<Expense>();
					_expensesBilledOn[invoiceId].Add(expense);
				}
			}
		}

		// ---- Access's clients and jobs ---------------------------------------------

		// Access's clients and jobs overrule Harvest's. Each client made from
		// Harvest takes Access's name and id (as its code): the client Access
		// gives its jobs, else the one of the same name. Every other client
		// Access has - and still uses, or uses for a job - is added. Each job
		// Access has takes its client, division, PO, dates, project manager,
		// status, notes and budget, disbursement rate included. Access's own
		// rows that aren't clients (SMA, Others, its payment adjustment) are
		// left out. A PO that's just the job's number is Access's placeholder,
		// so Harvest's stays.
		private void FollowAccessJobs()
		{
			if (_accessClients.Count == 0 && _accessProjects.Count == 0)
			{
				return;
			}

			var notClients = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SMA", "Others", "SMAAdj" };
			var usedByJobs = _accessProjects.Select(p => NameKey(p.Client)).ToHashSet();
			// By name, active ones first: Access lists a client twice now and then.
			var accessClient = new Dictionary<string, AccessClient>();
			foreach (AccessClient c in _accessClients
				.Where(c => !notClients.Contains(c.Abbreviation ?? "") && !notClients.Contains(c.Name))
				.OrderBy(c => c.Inactive))
			{
				accessClient.TryAdd(NameKey(c.Name), c);
			}
			AccessClient ClientNamed(string name) => accessClient.GetValueOrDefault(NameKey(name));
			// The only one whose name ends in it after a dash: "Alberta
			// Infrastructure" is "Government of Alberta - Alberta Infrastructure".
			AccessClient ClientEndingIn(string name)
			{
				var ending = accessClient.Values
					.Where(c => c.Name.Contains(" - ") && NameKey(c.Name[(c.Name.LastIndexOf(" - ") + 3)..]) == NameKey(name))
					.ToList();
				return ending.Count == 1 ? ending[0] : null;
			}

			// Harvest's clients, as Access has them. Two Harvest clients that are
			// one in Access become one.
			var customerOf = new Dictionary<AccessClient, Customer>();
			var notInAccess = new List<string>();
			foreach (Customer customer in _customers.ToList())
			{
				var codes = _projects.Where(p => p.CustomerId == customer.Id).Select(p => p.FundingCode).ToHashSet();
				AccessClient match = _accessProjects
						.Where(p => codes.Contains(p.Job))
						.Select(p => ClientNamed(p.Client))
						.Where(c => c != null)
						.GroupBy(c => c)
						.OrderByDescending(g => g.Count())
						.Select(g => g.Key)
						.FirstOrDefault()
					?? ClientNamed(customer.Name)
					?? ClientEndingIn(customer.Name);
				if (match == null)
				{
					notInAccess.Add(customer.Name);
					continue;
				}
				if (customerOf.TryGetValue(match, out Customer same))
				{
					foreach (Project p in _projects.Where(p => p.CustomerId == customer.Id)) p.CustomerId = same.Id;
					_customers.Remove(customer);
					continue;
				}
				customer.Name = match.Name;
				customer.Code = match.Id;
				customerOf[match] = customer;
			}
			int added = 0;
			foreach (AccessClient c in accessClient.Values.Where(c => !customerOf.ContainsKey(c) && (!c.Inactive || usedByJobs.Contains(NameKey(c.Name)))))
			{
				var customer = new Customer { Id = NewId(), Name = c.Name, Code = c.Id, Currency = "CAD" };
				_customers.Add(customer);
				customerOf[c] = customer;
				added++;
			}

			// The jobs.
			var notImported = new List<string>();
			var unknownPms = new List<string>();
			var placeholderPo = new List<string>();
			int followed = 0;
			foreach (AccessProject job in _accessProjects)
			{
				List<Project> projects = _projects.Where(p => p.FundingCode == job.Job).ToList();
				if (projects.Count == 0)
				{
					notImported.Add(job.Job);
					continue;
				}
				AccessClient client = ClientNamed(job.Client);
				TeamMember pm = PersonNamed(job.ProjectManager);
				if (pm == null && !string.IsNullOrWhiteSpace(job.ProjectManager))
				{
					unknownPms.Add(job.ProjectManager);
				}
				bool placeholder = job.Po == job.Job;
				if (placeholder)
				{
					placeholderPo.Add(job.Job);
				}

				foreach (Project project in projects)
				{
					if (client != null && customerOf.TryGetValue(client, out Customer customer))
					{
						project.CustomerId = customer.Id;
					}
					project.Division = string.IsNullOrWhiteSpace(job.Division) ? project.Division : job.Division;
					if (!placeholder && !string.IsNullOrWhiteSpace(job.Po))
					{
						project.PoNumber = job.Po;
					}
					project.StartDate = job.Start ?? project.StartDate;
					project.EndDate = job.End ?? project.EndDate;
					project.Status = StatusOf(job.Status) ?? project.Status;
					if (pm != null)
					{
						project.ProjectManagerId = pm.Id;
					}
					if (!string.IsNullOrWhiteSpace(job.Notes))
					{
						project.Notes = job.Notes.Trim() + "\n" + project.Notes;
					}

					ProjectFinancials financials = _financials.FirstOrDefault(f => f.ProjectId == project.Id);
					if (financials == null)
					{
						financials = new ProjectFinancials
						{
							Id = NewId(),
							ProjectId = project.Id,
							UpdatedAt = DateTime.UtcNow,
							UpdatedBy = _admin.Id,
						};
						_financials.Add(financials);
					}
					financials.ContractValue = job.ContractValue;
					financials.ChangeOrder = job.ChangeOrder;
					financials.MajorDisbursement = job.MajorDisbursement;
					financials.ProjectDisbursement = job.ProjectDisbursement;
					financials.Subconsultant = job.Subconsultant;
					financials.TotalBudget = job.TotalBudget;
					if (job.DisbursementRate.HasValue)
					{
						financials.DisbursementRate = Math.Round(job.DisbursementRate.Value * 100, 2);
						_rateFromAccessJob.Add(project.Id);
					}
				}
				followed++;
			}

			Warn($"Access: {customerOf.Count} clients ({added} not in Harvest); {followed} of its {_accessProjects.Count} jobs - "
				+ "client, division, PO, dates, project manager, status, notes and budget taken from them");
			if (notInAccess.Count > 0)
			{
				Warn($"Access: clients from Harvest it doesn't have, kept as Harvest names them: {string.Join(", ", notInAccess)}");
			}
			if (notImported.Count > 0)
			{
				Warn($"Access: its jobs {string.Join(", ", notImported)} weren't imported from Harvest (no time since {_from:MMM d, yyyy})");
			}
			if (unknownPms.Count > 0)
			{
				Warn($"Access: project managers not found among the team, so Harvest's stay: {string.Join(", ", unknownPms.Distinct())}");
			}
			if (placeholderPo.Count > 0)
			{
				Warn($"Access: jobs whose PO is just their job number, so Harvest's PO stays: {string.Join(", ", placeholderPo)}");
			}
		}

		// A client's name to match on: no "Ltd." or "Inc." and the like, nothing
		// in brackets, letters and digits only - "AECOM Canada ULC" and "AECOM
		// Canada Ltd." are one.
		private static string NameKey(string name)
		{
			string s = Regex.Replace((name ?? "").ToLowerInvariant(), @"\([^)]*\)", " ");
			s = Regex.Replace(s, @"\b(ltd|limited|ulc|inc|incorporated|corp|corporation|lp|llp)\b\.?", " ");
			return Regex.Replace(s, "[^a-z0-9]", "");
		}

		// A team member by name: the whole name; else the only one with that
		// last name ("Siri Fernando" is Siyaguna Fernando), or with it and a
		// first name starting the same ("Jeff King" is Jeffrey, not Lucy, King).
		private TeamMember PersonNamed(string name)
		{
			string full = Regex.Replace((name ?? "").Trim(), @"\s+", " ");
			if (full.Length == 0) return null;
			TeamMember exact = _people.FirstOrDefault(p => string.Equals(p.Name, full, StringComparison.OrdinalIgnoreCase));
			if (exact != null) return exact;

			string first = full.Split(' ')[0], last = full.Split(' ').Last();
			var sameLast = _people
				.Where(p => string.Equals((p.Name ?? "").Trim().Split(' ').Last(), last, StringComparison.OrdinalIgnoreCase))
				.ToList();
			if (sameLast.Count == 1) return sameLast[0];
			var sameStart = sameLast
				.Where(p =>
				{
					string theirs = (p.Name ?? "").Trim().Split(' ')[0];
					return theirs.StartsWith(first, StringComparison.OrdinalIgnoreCase)
						|| first.StartsWith(theirs, StringComparison.OrdinalIgnoreCase);
				})
				.ToList();
			return sameStart.Count == 1 ? sameStart[0] : null;
		}

		// Access's job status as the app's (the frontend's PROJECT_STATUSES).
		private static string StatusOf(string accessStatus) =>
			(accessStatus ?? "").Trim().ToLowerInvariant() switch
			{
				"in progress" => "In process",
				"finished" or "complete" or "completed" or "closed" => "Finished",
				"cancelled" or "canceled" => "Cancelled",
				_ => null,
			};

		// ---- Invoices -------------------------------------------------------------

		// Issued, with Harvest's lines and amounts as they were, linked to the
		// time and expenses they billed. Billed to the company for a company's
		// job; a job's client is set by hand, so its "Bill to" stays empty.
		//
		// Each one bills the subprojects its lines and time name - one of a job's,
		// or several of a company's jobs' - each line marked with its project. A
		// line with no project goes with the project it billed most.
		//
		// One whose joined number is all numbers Harvest also has on their own
		// ("50375038" beside "5037" and "5038") sums those invoices up - the same
		// billing again - and is left out. Another joined one ("47384739") is an
		// invoice per job, as Access has them, and is split (see Pieces). An
		// empty one - no lines, no amount - was never sent, and is left out too.
		//
		// What Access has overrules Harvest (see FollowAccess). An invoice from
		// before Access's first month counts as paid; one from then on that
		// Access doesn't have, as not paid. Without Access's, all count as paid.
		private void MakeInvoices(List<JsonElement> invoices, List<JsonElement> entries, List<JsonElement> expenses)
		{
			// Harvest's plain numbers, which a joined one never takes, and the
			// lowest of all its numbers, which spare ones go below (see NumberFor).
			var numbered = invoices.Select(i => (Invoice: i, Numbers: HarvestNumbers(Str(i, "number")))).ToList();
			var plain = numbered.Where(x => x.Numbers.Count == 1).Select(x => x.Numbers[0]).ToHashSet();
			int spare = numbered.SelectMany(x => x.Numbers).DefaultIfEmpty(1).Min();
			var used = new HashSet<int>();
			DateOnly? accessFrom = _access.Values.Select(a => a.InvoiceDate).Where(d => d != null).Min();
			int offTotal = 0;
			int combined = 0;
			int guessed = 0;
			int empty = 0;
			int split = 0;
			int rebuilt = 0;
			var fromAccess = new HashSet<int>();
			var notInAccess = new List<int>();
			var otherJob = new List<int>();
			Dictionary<string, Customer> clientOf = _customers.ToDictionary(c => c.Id);

			List<JsonElement> summaries = numbered
				.Where(x => x.Numbers.Count > 1 && x.Numbers.All(plain.Contains))
				.Select(x => x.Invoice)
				.ToList();
			var summaryIds = summaries.Select(i => Long(i, "id")).ToHashSet();
			if (summaries.Count > 0)
			{
				int billedOnSummaries = summaryIds.Sum(id => _entriesBilledOn.TryGetValue(id, out var on) ? on.Count : 0);
				Warn($"Invoices: {summaries.Count} left out as summaries of others (e.g. {Str(summaries[0], "number")}), " +
					$"{summaries.Sum(i => Dec(i, "amount")):N2} in all" +
					(billedOnSummaries > 0 ? $"; {billedOnSummaries} time entries were billed on them" : ""));
			}

			foreach (JsonElement inv in invoices.Where(i => !summaryIds.Contains(Long(i, "id"))).OrderBy(i => Str(i, "issue_date")))
			{
				var lines = inv.GetProperty("line_items").EnumerateArray()
					.Select(l => (Line: l, Subproject: LineSubproject(l)))
					.ToList();
				if (lines.Count == 0 && Dec(inv, "amount") == 0)
				{
					empty++;
					continue;
				}
				if (!_projectsOfClient.TryGetValue(IdOf(inv, "client"), out List<Project> clientProjects))
				{
					Warn($"Invoice {Str(inv, "number")}: its Harvest client \"{Str(inv.GetProperty("client"), "name")}\" isn't an imported project");
					continue;
				}

				long harvestId = Long(inv, "id");
				List<TimeEntry> allBilled = _entriesBilledOn.TryGetValue(harvestId, out var list) ? list : new List<TimeEntry>();
				List<Expense> allExpenses = _expensesBilledOn.TryGetValue(harvestId, out var listed) ? listed : new List<Expense>();
				Project ProjectOf(ProjectTask sub) => sub == null ? null : _projects.First(p => p.Id == sub.ProjectId);
				string original = Str(inv, "number");
				List<InvoicePiece> pieces = Pieces(original, plain, lines, allBilled, allExpenses);
				if (pieces.Count > 1) split++;

				foreach (InvoicePiece piece in pieces)
				{
					List<TimeEntry> billed = piece.Billed;

					// The project it billed most: by its lines, else by the time
					// billed, else the client's job - its only one, or a guess.
					Project main = piece.Lines.Where(l => l.Subproject != null)
							.GroupBy(l => l.Subproject.ProjectId)
							.OrderByDescending(g => g.Sum(l => Dec(l.Line, "amount")))
							.Select(g => clientProjects.FirstOrDefault(p => p.Id == g.Key))
							.FirstOrDefault(p => p != null)
						?? billed.GroupBy(e => e.ProjectId)
							.OrderByDescending(g => g.Sum(e => e.Hours))
							.Select(g => clientProjects.FirstOrDefault(p => p.Id == g.Key))
							.FirstOrDefault(p => p != null);
					if (main == null)
					{
						main = clientProjects[0];
						if (clientProjects.Count > 1) guessed++;
					}

					// The subprojects it billed: named on its lines, or by its time;
					// failing both, all of the project's.
					List<string> taskIds = piece.Lines.Where(l => l.Subproject != null).Select(l => l.Subproject.Id)
						.Concat(billed.Select(e => e.ProjectTaskId))
						.Where(id => id != null)
						.Distinct()
						.ToList();
					if (taskIds.Count == 0)
					{
						taskIds = _subprojects.Where(t => t.ProjectId == main.Id).Select(t => t.Id).ToList();
					}
					List<string> projectIds = new[] { main.Id }
						.Concat(_subprojects.Where(t => taskIds.Contains(t.Id)).Select(t => t.ProjectId))
						.Distinct()
						.ToList();
					if (projectIds.Count > 1) combined++;

					DateOnly issued = Day(Str(inv, "issue_date"));
					DateOnly? due = Str(inv, "due_date") is string d ? Day(d) : (DateOnly?)null;
					int number = piece.Number is int n && used.Add(n) ? n : NumberFor(original, plain, used, ref spare);

					// Harvest's period, kept when it isn't just the month it ends
					// in. Without one, the month billed: SMA bills after a month
					// ends, so sent in a month's first half, the month before.
					DateOnly? periodStart = Str(inv, "period_start") is string ps ? Day(ps) : (DateOnly?)null;
					DateOnly? periodEnd = Str(inv, "period_end") is string pe ? Day(pe) : (DateOnly?)null;
					DateOnly month = MonthEnd(periodEnd ?? (issued.Day <= 15 ? issued.AddMonths(-1) : issued));
					bool wholeMonth = periodStart == null || periodEnd == null || periodStart > periodEnd
						|| (periodStart == new DateOnly(month.Year, month.Month, 1) && periodEnd == month);

					var invoice = new Invoice
					{
						Id = NewId(),
						ProjectId = main.Id,
						ProjectIds = projectIds,
						ProjectTaskIds = taskIds,
						Month = month,
						PeriodStart = wholeMonth ? null : periodStart,
						PeriodEnd = wholeMonth ? null : periodEnd,
						Number = number,
						CreatedAt = When(Str(inv, "created_at")),
						CreatedBy = _admin.Id,
						Issued = true,
						IssuedAt = Str(inv, "sent_at") is string sent ? When(sent) : issued.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc),
						IssuedBy = _admin.Id,
						Paid = accessFrom == null || month < accessFrom.Value,
						InvoiceDate = issued,
						DueDate = due,
						Terms = due.HasValue ? due.Value.DayNumber - issued.DayNumber : (int?)null,
						PoNumber = Str(inv, "purchase_order") ?? main.PoNumber,
						ProjectName = main.Name,
						Subject = Str(inv, "subject"),
						BillFrom = "S.M.A. Consulting Ltd.",
						BillTo = main.CustomerId != null && clientOf.TryGetValue(main.CustomerId, out Customer client) ? client.Name : "",
						Notes = string.Join("\n", new[]
						{
							Str(inv, "notes")?.Trim(),
							original == number.ToString(CultureInfo.InvariantCulture) ? "Imported from Harvest."
								: string.IsNullOrWhiteSpace(original) ? "Imported from Harvest, where it has no number."
								: $"Imported from Harvest, where its number is {original}.",
						}.Where(x => !string.IsNullOrEmpty(x))),
						Eft = "",
					};

					foreach (var (line, sub) in piece.Lines)
					{
						invoice.Lines.Add(new InvoiceLine
						{
							Id = NewLineId(),
							ProjectId = (ProjectOf(sub) ?? main).Id,
							ItemType = Str(line, "kind"),
							Description = Str(line, "description"),
							Quantity = DecOrNull(line, "quantity"),
							Rate = DecOrNull(line, "unit_price"),
							Amount = Math.Round(Dec(line, "amount"), 2),
						});
					}
					if (Dec(inv, "tax") > 0) invoice.Taxes.Add(new InvoiceTax { Id = NewLineId(), Label = "GST", Percent = Dec(inv, "tax") });
					if (Dec(inv, "tax2") > 0) invoice.Taxes.Add(new InvoiceTax { Id = NewLineId(), Label = "PST", Percent = Dec(inv, "tax2") });

					// Same total as Harvest's, to the cent? (The app's taxes are a
					// percentage of the subtotal.) A split one's is only part of it.
					if (pieces.Count == 1 && Math.Abs(TotalOf(invoice) - Dec(inv, "amount")) > 0.05m)
					{
						offTotal++;
					}

					if (_access.TryGetValue(number, out AccessInvoice inAccess))
					{
						fromAccess.Add(number);
						if (FollowAccess(invoice, inAccess, main)) rebuilt++;
						if (!string.Equals(inAccess.Job?.Trim(), main.FundingCode, StringComparison.OrdinalIgnoreCase))
						{
							otherJob.Add(number);
						}
					}
					else if (accessFrom != null && month >= accessFrom.Value)
					{
						notInAccess.Add(number);
					}

					foreach (TimeEntry entry in billed)
					{
						entry.Invoiced = true;
						entry.InvoiceId = invoice.Id;
					}
					invoice.TimeEntries = billed.Select(UserTimeEntry.From).ToList();
					invoice.BilledExpenses = piece.Expenses;
					invoice.BilledAssignments = _assignments.Where(a => projectIds.Contains(a.ProjectId)).ToList();
					invoice.BilledRates = RatesFor(projectIds);
					invoice.BilledTasks = _subprojects.Where(t => projectIds.Contains(t.ProjectId)).ToList();
					invoice.BilledSubTasks = _costCodes.Where(t => projectIds.Contains(t.ProjectId)).ToList();

					_invoices.Add(invoice);
				}
			}

			if (empty > 0)
			{
				Warn($"Invoices: {empty} empty ones (no lines, nothing billed) left out");
			}
			if (split > 0)
			{
				Warn($"Invoices: {split} with joined numbers split into an invoice per job");
			}
			if (combined > 0)
			{
				Warn($"Invoices: {combined} bill several jobs at once");
			}
			if (guessed > 0)
			{
				Warn($"Invoices: {guessed} name no job on any line and billed no time - put on their client's first job");
			}
			if (offTotal > 0)
			{
				Warn($"Invoices: {offTotal} don't total exactly what Harvest says (tax rounding or tax lines)");
			}
			if (_access.Count > 0)
			{
				Warn($"Access: {fromAccess.Count} of its {_access.Count} invoices matched - paid, payment date, month and date taken from them; "
					+ $"{rebuilt} had their lines replaced with Access's figures, Harvest's not adding up to them. "
					+ $"Invoices before {accessFrom:yyyy-MM} count as paid");
				List<int> missing = _access.Keys.Where(k => !fromAccess.Contains(k)).OrderBy(k => k).ToList();
				if (missing.Count > 0)
				{
					Warn($"Access: {missing.Count} of its invoices aren't in Harvest: {string.Join(", ", missing.Take(30))}");
				}
				if (notInAccess.Count > 0)
				{
					Warn($"Access: {notInAccess.Count} Harvest invoices from its months aren't in it, so count as not paid: {string.Join(", ", notInAccess.OrderBy(k => k).Take(30))}");
				}
				if (otherJob.Count > 0)
				{
					Warn($"Access: {otherJob.Count} invoices are on another job there: {string.Join(", ", otherJob.Take(30))}");
				}
			}
			// Each subproject on one invoice for any day, as the app has it: in
			// order of their first day, an invoice starting on or before the
			// latest last day so far overlaps an earlier one.
			foreach (var g in _invoices
				.SelectMany(i => i.ProjectTaskIds.Select(t => (Task: t, Invoice: i)))
				.GroupBy(x => x.Task))
			{
				Invoice latest = null;
				foreach (Invoice i in g.Select(x => x.Invoice).OrderBy(i => i.From()))
				{
					if (latest != null && i.From() <= latest.To())
					{
						ProjectTask sub = _subprojects.First(t => t.Id == g.Key);
						Warn($"Invoices: {_projects.First(p => p.Id == sub.ProjectId).Name} - {sub.Name} is on invoices {latest.Number} and {i.Number}, whose periods overlap");
					}
					if (latest == null || i.To() > latest.To())
					{
						latest = i;
					}
				}
			}
		}

		// One app invoice from a Harvest invoice: its number (null for the usual
		// one, see NumberFor), lines, time and expenses.
		private record InvoicePiece(int? Number, List<(JsonElement Line, ProjectTask Subproject)> Lines,
			List<TimeEntry> Billed, List<Expense> Expenses);

		// A Harvest invoice as the app's: itself - or, joined from an invoice per
		// job ("47384739" for jobs 578 and 580, no number of it Harvest's own),
		// one per job, each with its own number: the one Access has for that
		// job, the rest in order of the jobs' codes. Each line, entry and
		// expense goes with its job; a line naming none, with the job it billed
		// most.
		private List<InvoicePiece> Pieces(string original, HashSet<int> plain,
			List<(JsonElement Line, ProjectTask Subproject)> lines, List<TimeEntry> billed, List<Expense> expenses)
		{
			var whole = new List<InvoicePiece> { new InvoicePiece(null, lines, billed, expenses) };
			List<int> parts = HarvestNumbers(original);
			if (parts.Count < 2 || parts.Any(plain.Contains))
			{
				return whole;
			}
			List<string> jobs = lines.Select(l => l.Subproject?.ProjectId)
				.Concat(billed.Select(e => e.ProjectId))
				.Where(id => id != null)
				.Distinct()
				.ToList();
			if (jobs.Count != parts.Count)
			{
				return whole;
			}

			string CodeOf(string projectId) => _projects.First(p => p.Id == projectId).FundingCode ?? "";
			var jobOf = new Dictionary<int, string>();
			foreach (int part in parts)
			{
				if (_access.TryGetValue(part, out AccessInvoice a)
					&& jobs.FirstOrDefault(j => CodeOf(j) == a.Job?.Trim() && !jobOf.ContainsValue(j)) is string job)
				{
					jobOf[part] = job;
				}
			}
			List<string> restJobs = jobs.Where(j => !jobOf.ContainsValue(j))
				.OrderBy(j => int.TryParse(CodeOf(j), out int code) ? code : int.MaxValue)
				.ThenBy(CodeOf)
				.ToList();
			List<int> restParts = parts.Where(p => !jobOf.ContainsKey(p)).OrderBy(p => p).ToList();
			for (int k = 0; k < restParts.Count; k++)
			{
				jobOf[restParts[k]] = restJobs[k];
			}

			string most = lines.Where(l => l.Subproject != null)
				.GroupBy(l => l.Subproject.ProjectId)
				.OrderByDescending(g => g.Sum(l => Dec(l.Line, "amount")))
				.Select(g => g.Key)
				.FirstOrDefault() ?? jobs[0];
			return parts
				.Select(part => new InvoicePiece(
					part,
					lines.Where(l => (l.Subproject?.ProjectId ?? most) == jobOf[part]).ToList(),
					billed.Where(e => e.ProjectId == jobOf[part]).ToList(),
					expenses.Where(e => e.ProjectId == jobOf[part]).ToList()))
				.ToList();
		}

		// Access's invoice overrules Harvest's: paid or not, and when; the month
		// billed, and the invoice's date, its last day; and - when Harvest's
		// lines don't come to Access's total - the lines, made again from
		// Access's labour, disbursement and adjustment (expenses), with its tax.
		// Returns whether the lines were.
		private bool FollowAccess(Invoice invoice, AccessInvoice access, Project main)
		{
			invoice.Paid = access.Paid;
			invoice.PaymentDate = access.Paid ? access.PaymentDate : null;
			if (access.InvoiceDate is DateOnly dated)
			{
				if (MonthEnd(dated) != invoice.Month)
				{
					// Harvest's days don't fall in Access's month: its whole month.
					invoice.Month = MonthEnd(dated);
					invoice.PeriodStart = null;
					invoice.PeriodEnd = null;
				}
				invoice.InvoiceDate = dated;
				invoice.Terms = invoice.DueDate >= dated ? invoice.DueDate.Value.DayNumber - dated.DayNumber : (int?)null;
			}

			decimal harvestTotal = TotalOf(invoice);
			if (Math.Abs(harvestTotal - access.Total) <= 0.05m)
			{
				return false;
			}
			invoice.Lines = new List<InvoiceLine>();
			void Add(string itemType, string description, decimal amount)
			{
				if (amount == 0) return;
				invoice.Lines.Add(new InvoiceLine
				{
					Id = NewLineId(),
					ProjectId = main.Id,
					ItemType = itemType,
					Description = description,
					Quantity = 1,
					Rate = Math.Round(amount, 2),
					Amount = Math.Round(amount, 2),
				});
			}
			Add("Service", $"{main.Name}: direct labour", access.Labour);
			Add("Disbursement", "Disbursement", access.Disbursement);
			Add("Expense", string.IsNullOrWhiteSpace(access.AdjustmentNote) ? "Expenses" : access.AdjustmentNote, access.Adjustment);
			decimal net = access.Labour + access.Disbursement + access.Adjustment;
			invoice.Taxes = access.Tax == 0 || net == 0
				? new List<InvoiceTax>()
				: new List<InvoiceTax> { new InvoiceTax { Id = NewLineId(), Label = "GST", Percent = Math.Round(access.Tax / net * 100, 3) } };
			invoice.Notes = string.Join("\n", new[]
			{
				invoice.Notes,
				$"Its lines are Access's figures: Harvest's came to {harvestTotal:N2}, Access's to {access.Total:N2}.",
			}.Where(x => !string.IsNullOrEmpty(x)));
			return true;
		}

		// What an invoice comes to, as the app adds it up: its lines, and each
		// tax a percentage of them.
		private static decimal TotalOf(Invoice invoice)
		{
			decimal subtotal = invoice.Lines.Sum(l => l.Amount ?? 0);
			return subtotal + invoice.Taxes.Sum(t => Math.Round(subtotal * (t.Percent ?? 0) / 100, 2));
		}

		// Each job's flat disbursement rate, from its latest invoice in Access:
		// its disbursement as a share of its labour, to the quarter percent (0
		// for none). A job Access has no invoice for keeps the standard rate.
		private void SetDisbursementRates()
		{
			var rates = new List<decimal>();
			var notImported = new List<string>();
			foreach (AccessInvoice latest in _access.Values
				.Where(a => a.Labour != 0 && !string.IsNullOrWhiteSpace(a.Job))
				.GroupBy(a => a.Job.Trim())
				.Select(g => g.OrderBy(a => a.InvoiceDate).ThenBy(a => a.Number).Last()))
			{
				decimal rate = Math.Round(latest.Disbursement / latest.Labour * 400, MidpointRounding.AwayFromZero) / 4;
				List<Project> jobs = _projects.Where(p => p.FundingCode == latest.Job.Trim()).ToList();
				if (jobs.Count == 0)
				{
					notImported.Add(latest.Job.Trim());
					continue;
				}
				foreach (Project job in jobs.Where(j => !_rateFromAccessJob.Contains(j.Id)))
				{
					ProjectFinancials financials = _financials.FirstOrDefault(f => f.ProjectId == job.Id);
					if (financials == null)
					{
						financials = new ProjectFinancials
						{
							Id = NewId(),
							ProjectId = job.Id,
							UpdatedAt = DateTime.UtcNow,
							UpdatedBy = _admin.Id,
						};
						_financials.Add(financials);
					}
					financials.DisbursementRate = rate;
					rates.Add(rate);
				}
			}
			if (rates.Count > 0)
			{
				Warn($"Access: disbursement rate set on {rates.Count} jobs from their latest invoice - "
					+ string.Join(", ", rates.GroupBy(r => r).OrderBy(g => g.Key).Select(g => $"{g.Key:0.##}%: {g.Count()}")));
			}
			if (notImported.Count > 0)
			{
				Warn($"Access: jobs {string.Join(", ", notImported)} have invoices there but weren't imported from Harvest");
			}
		}

		// The subproject a Harvest invoice line billed, when it names one that
		// was imported.
		private ProjectTask LineSubproject(JsonElement line) =>
			line.TryGetProperty("project", out JsonElement hp) && hp.ValueKind == JsonValueKind.Object
				&& _subprojectOf.TryGetValue(Long(hp, "id"), out ProjectTask sub)
				? sub
				: null;

		// Harvest's number when it's a plain one not used yet. Some Harvest
		// invoices carry several joined ("45964597": 4596 and 4597) and take the
		// first that no plain one has. Anything else gets one below all of
		// Harvest's, so the app's own numbers carry on from Harvest's highest.
		// The original is kept in the invoice's notes.
		private static int NumberFor(string original, HashSet<int> plain, HashSet<int> used, ref int spare)
		{
			List<int> numbers = HarvestNumbers(original);
			int number = numbers.Count == 1
				? numbers[0]
				: numbers.FirstOrDefault(n => !plain.Contains(n) && !used.Contains(n));
			if (number <= 0 || !used.Add(number))
			{
				while (!used.Add(--spare)) { }
				number = spare;
			}
			return number;
		}

		// The numbers a Harvest invoice number stands for: "5037" is 5037, and
		// "50375038" - several joined, 4 digits each - is 5037 and 5038.
		private static List<int> HarvestNumbers(string original)
		{
			string digits = (original ?? "").Trim();
			if (Regex.IsMatch(digits, @"^\d{1,6}$"))
			{
				int number = int.Parse(digits, CultureInfo.InvariantCulture);
				return number > 0 ? new List<int> { number } : new List<int>();
			}
			if (Regex.IsMatch(digits, @"^\d{8,}$") && digits.Length % 4 == 0)
			{
				return Enumerable.Range(0, digits.Length / 4)
					.Select(k => int.Parse(digits.Substring(k * 4, 4), CultureInfo.InvariantCulture))
					.Where(n => n > 0)
					.ToList();
			}
			return new List<int>();
		}

		// ---- Names ------------------------------------------------------------------

		// "[493] VLW Construction Phase (Project Number:60649184)" -> "493".
		private static string JobCode(string clientName)
		{
			Match m = Regex.Match(clientName ?? "", @"^\s*\[([^\]]+)\]");
			return m.Success ? m.Groups[1].Value.Trim() : null;
		}

		// ... -> "VLW Construction Phase".
		private static string JobName(string clientName) =>
			Collapse(Regex.Replace(Regex.Replace(clientName ?? "", @"^\s*\[[^\]]*\]\s*", ""),
				@"\s*\((project\s*number|PO)\s*:[^)]*\)", "", RegexOptions.IgnoreCase));

		// "0100 MST PM and Admin 2026/2027 (PO:1688557) (Project number:60649184)"
		// -> "0100 MST PM and Admin".
		private static string SubprojectName(string projectName) =>
			Collapse(Regex.Replace(Regex.Replace(projectName ?? "",
				@"\s*\((PO|project\s*number)\s*:[^)]*\)", "", RegexOptions.IgnoreCase),
				@"\s*\b(19|20)\d{2}\s*[/-]\s*((19|20)\d{2}|\d{2})\b", ""));

		private static string PoOf(string projectName)
		{
			Match m = Regex.Match(projectName ?? "", @"\(\s*PO\s*:\s*([^)]+)\)", RegexOptions.IgnoreCase);
			return m.Success ? m.Groups[1].Value.Trim() : null;
		}

		// What a list of codes all start with: "A01-04", "A01-05" -> "A01".
		private static string CommonPrefix(List<string> codes)
		{
			if (codes.Count == 0) return null;
			string prefix = codes[0];
			foreach (string code in codes)
			{
				while (!code.StartsWith(prefix, StringComparison.Ordinal)) prefix = prefix[..^1];
			}
			prefix = prefix.TrimEnd('-', ' ', '.');
			return prefix.Length > 0 ? prefix : null;
		}

		private static string Collapse(string text) => Regex.Replace(text, @"\s{2,}", " ").Trim();

		// ---- Harvest ----------------------------------------------------------------

		// Every page of a list. Harvest allows 100 requests in 15 seconds; on a
		// 429 it says how long to wait.
		private List<JsonElement> All(string path, string key)
		{
			var rows = new List<JsonElement>();
			int? page = 1;
			while (page != null)
			{
				string separator = path.Contains('?') ? "&" : "?";
				using HttpResponseMessage res = _http.GetAsync($"{Api}{path}{separator}per_page=2000&page={page}").Result;
				if ((int)res.StatusCode == 429)
				{
					Thread.Sleep(res.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(15));
					continue;
				}
				string body = res.Content.ReadAsStringAsync().Result;
				if (!res.IsSuccessStatusCode)
				{
					throw new InvalidOperationException($"Harvest {path}: {(int)res.StatusCode} {body}");
				}
				using JsonDocument doc = JsonDocument.Parse(body);
				foreach (JsonElement row in doc.RootElement.GetProperty(key).EnumerateArray())
				{
					rows.Add(row.Clone());
				}
				JsonElement next = doc.RootElement.GetProperty("next_page");
				page = next.ValueKind == JsonValueKind.Number ? next.GetInt32() : null;
			}
			return rows;
		}

		private static string Str(JsonElement e, string name) =>
			e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

		private static long Long(JsonElement e, string name) => e.GetProperty(name).GetInt64();

		// The id of a nested object: e.g. "project": { "id": 123, ... }.
		private static long IdOf(JsonElement e, string name) =>
			e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Object ? v.GetProperty("id").GetInt64() : 0;

		private static bool Bool(JsonElement e, string name) =>
			e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.True;

		private static decimal Dec(JsonElement e, string name) => DecOrNull(e, name) ?? 0;

		private static decimal? DecOrNull(JsonElement e, string name) =>
			e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;

		private static DateOnly Day(string iso) => DateOnly.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);

		private static DateTime When(string iso) =>
			DateTime.Parse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

		private static DateOnly MonthEnd(DateOnly day) => new DateOnly(day.Year, day.Month, DateTime.DaysInMonth(day.Year, day.Month));

		private static string NewId() => ObjectId.GenerateNewId().ToString();

		private static string NewLineId() => Guid.NewGuid().ToString("N").Substring(0, 12);

		private void Warn(string text) => _warnings.Add(text);
	}
}
