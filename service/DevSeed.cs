using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace sma.plan
{
	// Test data for load testing: a whole company's worth of made-up people,
	// clients, projects, time, expenses, invoices and reimbursement batches, so
	// pages can be profiled at production scale. Dev only, and only ever into a
	// database whose name ends in LOADTEST (see Enabled) - it empties that
	// database first.
	//
	// The same options always make the same data (a fixed random seed), so a
	// page can be profiled before and after a change on identical data. It's
	// written with the server's own models, so it has the shape the app expects.
	internal static class DevSeed
	{
		public const int MaxPeople = 300;
		public const int MaxYears = 5;

		// On only where ENABLE_DEV_SEED is "true" and the database is a LOADTEST
		// one. Never set ENABLE_DEV_SEED in Azure.
		public static bool Enabled()
		{
			return string.Equals(Environment.GetEnvironmentVariable("ENABLE_DEV_SEED"), "true", StringComparison.OrdinalIgnoreCase)
				&& DatabaseName().EndsWith("LOADTEST", StringComparison.OrdinalIgnoreCase);
		}

		public static string DatabaseName()
		{
			return Environment.GetEnvironmentVariable("DATABASE_NAME") ?? "";
		}

		// Empties every collection the generator fills. The collections - and
		// their indexes - stay; holidays are left as they are.
		public static void Wipe()
		{
			if (!Enabled())
			{
				throw new InvalidOperationException("Test data is only ever written to a LOADTEST database.");
			}

			Clear<User>();
			Clear<TeamMember>();
			Clear<Customer>();
			Clear<ProjectRole>();
			Clear<Project>();
			Clear<ProjectTask>();
			Clear<ProjectSubTask>();
			Clear<ProjectAssignment>();
			Clear<ProjectBillingRate>();
			Clear<ProjectFinancials>();
			Clear<TimeEntry>();
			Clear<Expense>();
			Clear<ExpenseReceipt>();
			Clear<Invoice>();
			Clear<ReimbursementBatch>();
		}

		private static void Clear<T>() where T : GeneralModel
		{
			Database.GetCollection<T>().DeleteMany(FilterDefinition<T>.Empty);
		}
	}

	// What a test data run made.
	internal class DevSeedSummary
	{
		public int People { get; set; }
		public int Clients { get; set; }
		public int Projects { get; set; }
		public int TimeEntries { get; set; }
		public int Expenses { get; set; }
		public int Invoices { get; set; }
		public int ReimbursementBatches { get; set; }
		public double Seconds { get; set; }
	}

	// One run of the generator: people over `years` up to today. `timeOff` are
	// the paid hours off tasks by TimeOffType, `holidays` the days SMA takes off.
	internal class DevSeedGenerator
	{
		private const int InsertBatch = 5000;
		private const decimal Gst = 5;
		private const decimal FlatDisbursement = 3;

		private static readonly string[] FirstNames =
		{
			"Avery", "Blake", "Casey", "Dana", "Elliot", "Finley", "Gray", "Harper", "Indra", "Jordan",
			"Kai", "Logan", "Morgan", "Noor", "Oakley", "Parker", "Quinn", "Riley", "Sage", "Taylor",
			"Uma", "Val", "Wren", "Xia", "Yusuf", "Zane", "Amara", "Bruno", "Chen", "Dev",
			"Esme", "Farah", "Gus", "Hana", "Ivo", "Jada", "Kofi", "Lena", "Milo", "Nia",
		};

		private static readonly string[] LastNames =
		{
			"Anders", "Bishop", "Carver", "Doyle", "Ellis", "Fraser", "Grant", "Hale", "Ingram", "Joshi",
			"Keller", "Lam", "Moreau", "Nakamura", "Okafor", "Patel", "Quinlan", "Rossi", "Singh", "Tran",
			"Underwood", "Vance", "Walsh", "Xu", "Young", "Zhou", "Abbott", "Boyd", "Castro", "Diaz",
			"Evans", "Flores", "Gill", "Hughes", "Iqbal", "Jensen", "Khan", "Lopez", "Mehta", "Novak",
		};

		private static readonly string[] ClientNames =
		{
			"City of Northfield", "Prairie Rail Authority", "Lakeview Developments", "Aspen Ridge Homes",
			"Riverbend School Division", "Westgate Properties", "Northern Lights Utilities", "Cedar Creek County",
			"Summit Industrial", "Bluewater Energy", "Granite Peak Mining", "Maple Grove Health",
			"Harbour Logistics", "Silverline Transit", "Oakridge Municipal District", "Pinecrest Developments",
			"Redwater Holdings", "Clearview Telecom", "Stonebridge Construction", "Willow Park Estates",
			"Eastport Authority", "Golden Plains Agri", "Ironwood Manufacturing", "Juniper Hills Resort",
			"Kestrel Airport Authority", "Lone Pine Water Co-op", "Meridian Pipelines", "Northgate Campus",
			"Orchard Valley Town", "Parkland Recreation",
		};

		private static readonly string[] Places =
		{
			"Mill Creek", "Valley Line", "Whitemud", "Riverside", "Southgate", "Kingsway", "Terwillegar",
			"Clareview", "Lakeshore", "Hillcrest", "Beaumont", "Sherwood", "Old Strathcona", "Westmount",
			"Highlands", "Ellerslie", "Windermere", "Castle Downs", "Meadows", "Rabbit Hill",
		};

		private static readonly string[] Works =
		{
			"Bridge Rehabilitation", "Storm Sewer Upgrade", "Traffic Study", "Interchange Design",
			"Road Widening", "Water Main Replacement", "Site Servicing", "Drainage Assessment",
			"Transit Station", "Pedestrian Bridge", "Culvert Replacement", "Noise Wall",
			"Intersection Improvements", "Pavement Rehabilitation", "LRT Extension", "Pump Station",
		};

		private static readonly string[] SubprojectNames =
		{
			"Phase 1", "Phase 2", "Preliminary Design", "Detailed Design", "Construction Support",
			"Project Management", "Site Inspection", "Reporting",
		};

		private static readonly string[] TaskNames =
		{
			"Drawings", "Calculations", "Site visit", "Meetings", "Report writing", "QA/QC",
			"Coordination", "Field work", "Specifications", "Cost estimate",
		};

		private static readonly string[] Divisions = { "JK", "TK", "MA", "HP" };

		// Code, name, base hourly rate, share of people.
		private static readonly (string Code, string Name, decimal Rate, int Weight)[] Roles =
		{
			("PM", "Project Manager", 165, 15),
			("SE", "Senior Engineer", 150, 20),
			("EN", "Engineer", 125, 30),
			("ET", "Engineering Technologist", 105, 15),
			("DR", "Drafter", 95, 12),
			("AD", "Project Administrator", 75, 8),
		};

		// Category, lowest and highest amount.
		private static readonly (string Category, int Low, int High)[] ExpenseKinds =
		{
			("Mileage", 20, 250), ("Meals", 15, 120), ("Parking", 5, 40), ("Accommodation", 120, 450),
			("Airfare", 250, 1200), ("Taxi / Transit", 15, 80), ("Printing", 10, 200), ("Supplies", 10, 300),
			("Equipment", 50, 900), ("Other", 10, 200),
		};

		private readonly int _peopleCount;
		private readonly string _callerEmail;
		private readonly string _callerName;
		private readonly Dictionary<string, ProjectSubTask> _timeOff;
		private readonly HashSet<DateOnly> _holidays;
		private readonly Random _random;
		private readonly DateOnly _today;
		private readonly DateOnly _start;

		private readonly List<User> _users = new();
		private readonly List<TeamMember> _people = new();
		private readonly List<Customer> _clients = new();
		private readonly List<ProjectRole> _roles = new();
		private readonly List<Project> _projects = new();
		private readonly List<ProjectTask> _subprojects = new();
		private readonly List<ProjectSubTask> _tasks = new();
		private readonly List<ProjectAssignment> _assignments = new();
		private readonly List<ProjectBillingRate> _rates = new();
		private readonly List<ProjectFinancials> _financials = new();
		private readonly List<TimeEntry> _entries = new();
		private readonly List<Expense> _expenses = new();
		private readonly List<Invoice> _invoices = new();
		private readonly List<ReimbursementBatch> _batches = new();

		// Each person's primary role, and the admin who reviews and files.
		private readonly Dictionary<string, ProjectRole> _roleOf = new();
		private User _admin;

		public DevSeedGenerator(int people, int years, string callerEmail, string callerName,
			Dictionary<string, ProjectSubTask> timeOff, IEnumerable<DateOnly> holidays)
		{
			_peopleCount = people;
			_callerEmail = callerEmail;
			_callerName = callerName;
			_timeOff = timeOff;
			_holidays = holidays.ToHashSet();
			// Same options, same data.
			_random = new Random(20261023 + people * 31 + years);
			_today = DateOnly.FromDateTime(DateTime.Today);
			_start = new DateOnly(_today.Year - years, _today.Month, 1);
		}

		public DevSeedSummary Run()
		{
			var clock = System.Diagnostics.Stopwatch.StartNew();

			MakeRoles();
			MakePeople();
			MakeClients();
			MakeProjects();
			MakeTime();
			MakeExpenses();
			MakeInvoices();
			MakeReimbursements();

			Insert(_users);
			Insert(_people);
			Insert(_clients);
			Insert(_roles);
			Insert(_projects);
			Insert(_subprojects);
			Insert(_tasks);
			Insert(_assignments);
			Insert(_rates);
			Insert(_financials);
			Insert(_entries);
			Insert(_expenses);
			Insert(_invoices);
			Insert(_batches);

			return new DevSeedSummary
			{
				People = _people.Count,
				Clients = _clients.Count,
				Projects = _projects.Count,
				TimeEntries = _entries.Count,
				Expenses = _expenses.Count,
				Invoices = _invoices.Count,
				ReimbursementBatches = _batches.Count,
				Seconds = Math.Round(clock.Elapsed.TotalSeconds, 1),
			};
		}

		// ---- People and reference data -------------------------------------

		private void MakeRoles()
		{
			foreach (var role in Roles)
			{
				_roles.Add(new ProjectRole { Id = NewId(), Code = role.Code, Name = role.Name });
			}
		}

		// The caller first - as an admin and project manager, so they land in
		// the app signed in as themselves - then made-up colleagues.
		private void MakePeople()
		{
			for (int i = 0; i < _peopleCount; i++)
			{
				bool caller = i == 0;
				string first = FirstNames[i % FirstNames.Length];
				string last = LastNames[(i / FirstNames.Length + i) % LastNames.Length];
				string name = caller ? _callerName : $"{first} {last}";
				string email = caller ? _callerEmail : $"{first}.{last}{i}@loadtest.sma".ToLowerInvariant();

				var user = new User { Id = NewId(), Email = email, DisplayName = name };
				// The caller and one colleague are admins, as in a real office.
				if (i <= 1)
				{
					user.Privileges.Add(User.AdminPrivilege);
				}
				_users.Add(user);

				ProjectRole role = caller ? _roles[0] : PickRole();
				var person = new TeamMember
				{
					Id = NewId(),
					Name = name,
					UserId = user.Id,
					AssignedRoles = new List<string> { role.Id },
					PercentAvailable = 100,
				};
				_people.Add(person);
				_roleOf[person.Id] = role;

				if (caller)
				{
					_admin = user;
				}
			}
		}

		private ProjectRole PickRole()
		{
			int roll = _random.Next(Roles.Sum(r => r.Weight));
			for (int i = 0; i < Roles.Length; i++)
			{
				roll -= Roles[i].Weight;
				if (roll < 0)
				{
					return _roles[i];
				}
			}
			return _roles[^1];
		}

		private void MakeClients()
		{
			int count = Math.Max(8, _peopleCount / 2);
			for (int i = 0; i < count; i++)
			{
				string name = ClientNames[i % ClientNames.Length] + (i >= ClientNames.Length ? $" {i / ClientNames.Length + 1}" : "");
				string code = new string(name.Split(' ').Where(w => w.Length > 0 && char.IsLetter(w[0])).Select(w => char.ToUpperInvariant(w[0])).ToArray());
				_clients.Add(new Customer { Id = NewId(), Name = name, Code = $"{code}{i + 1}", Currency = "CAD" });
			}
		}

		// ---- Projects ---------------------------------------------------------

		private void MakeProjects()
		{
			List<TeamMember> managers = _people.Where(p => _roleOf[p.Id].Code == "PM").ToList();
			if (managers.Count == 0)
			{
				managers.Add(_people[0]);
			}

			int count = Math.Max(5, (int)Math.Round(_peopleCount * 1.2));
			int months = MonthsBetween(_start, _today) + 1;

			for (int i = 0; i < count; i++)
			{
				// Some started before the period, some run past today.
				DateOnly start = _start.AddMonths(_random.Next(-6, months)).AddDays(_random.Next(0, 27));
				int length = _random.Next(3, 19);
				DateOnly end = start.AddMonths(length);
				bool ongoing = end > _today && _random.NextDouble() < 0.7;

				int typeRoll = _random.Next(100);
				string type = typeRoll < 70 ? Project.TypeHourly : typeRoll < 90 ? Project.TypeLumpSum : Project.TypeNonBillable;
				string status = end < _today
					? (_random.NextDouble() < 0.9 ? Project.StatusFinished : Project.StatusCancelled)
					: Project.StatusInProcess;

				TeamMember manager = managers[_random.Next(managers.Count)];
				var project = new Project
				{
					Id = NewId(),
					CustomerId = _clients[_random.Next(_clients.Count)].Id,
					Name = $"{Places[_random.Next(Places.Length)]} {Works[_random.Next(Works.Length)]}",
					FundingCode = $"{start.Year % 100:00}-{i + 1:000}",
					Division = Divisions[_random.Next(Divisions.Length)],
					PoNumber = _random.NextDouble() < 0.6 ? _random.Next(100000000, 999999999).ToString(CultureInfo.InvariantCulture) : null,
					StartDate = start,
					EndDate = ongoing ? null : end,
					ProjectType = type,
					Billable = type != Project.TypeNonBillable,
					Status = status,
					ProjectManagerId = manager.Id,
				};
				_projects.Add(project);

				MakeStructure(project);
				MakeTeam(project, manager);
				MakeRates(project);
				MakeFinancials(project, length);
			}

			// Somewhere for everyone's internal time between projects.
			var internalWork = new Project
			{
				Id = NewId(),
				Name = "SMA Internal - Admin & Training",
				FundingCode = "SMA-000",
				ProjectType = Project.TypeNonBillable,
				Billable = false,
				Status = Project.StatusSma,
				StartDate = _start,
				ProjectManagerId = _people[0].Id,
			};
			_projects.Add(internalWork);
			MakeStructure(internalWork);
			foreach (TeamMember person in _people)
			{
				Assign(internalWork, person, _roleOf[person.Id]);
			}
		}

		private void MakeStructure(Project project)
		{
			int subprojects = _random.Next(2, 5);
			foreach (string subprojectName in SubprojectNames.OrderBy(_ => _random.Next()).Take(subprojects))
			{
				var subproject = new ProjectTask { Id = NewId(), ProjectId = project.Id, Name = subprojectName };
				_subprojects.Add(subproject);

				foreach (string taskName in TaskNames.OrderBy(_ => _random.Next()).Take(_random.Next(2, 6)))
				{
					_tasks.Add(new ProjectSubTask
					{
						Id = NewId(),
						ProjectId = project.Id,
						ProjectTaskId = subproject.Id,
						Name = taskName,
						Billable = project.Billable && _random.NextDouble() < 0.9,
					});
				}
			}
		}

		private void MakeTeam(Project project, TeamMember manager)
		{
			Assign(project, manager, _roles[0]);
			foreach (TeamMember person in _people.Where(p => p.Id != manager.Id).OrderBy(_ => _random.Next()).Take(_random.Next(3, 10)))
			{
				Assign(project, person, _roleOf[person.Id]);
			}
		}

		private void Assign(Project project, TeamMember person, ProjectRole role)
		{
			_assignments.Add(new ProjectAssignment
			{
				Id = NewId(),
				ProjectId = project.Id,
				TeamMemberId = person.Id,
				ProjectRoleId = role.Id,
				StartDate = project.StartDate ?? _start,
				EndDate = project.EndDate,
			});
		}

		// One rate per role on the team; a project running over a new year gets
		// a raise from January 1, the old rate closed the day before.
		private void MakeRates(Project project)
		{
			DateOnly start = project.StartDate ?? _start;
			DateOnly raise = new DateOnly(start.Year + 1, 1, 1);
			bool raised = (project.EndDate ?? _today) >= raise;

			foreach (string roleId in _assignments.Where(a => a.ProjectId == project.Id).Select(a => a.ProjectRoleId).Distinct())
			{
				decimal baseRate = Roles[_roles.FindIndex(r => r.Id == roleId)].Rate;
				decimal rate = Math.Round(baseRate * (decimal)(0.9 + _random.NextDouble() * 0.2), 0);
				_rates.Add(new ProjectBillingRate
				{
					Id = NewId(),
					ProjectId = project.Id,
					ProjectRoleId = roleId,
					HourlyRate = rate,
					StartDate = start,
					EndDate = raised ? raise.AddDays(-1) : null,
				});
				if (raised)
				{
					_rates.Add(new ProjectBillingRate
					{
						Id = NewId(),
						ProjectId = project.Id,
						ProjectRoleId = roleId,
						HourlyRate = Math.Round(rate * 1.04m, 0),
						StartDate = raise,
					});
				}
			}
		}

		private void MakeFinancials(Project project, int months)
		{
			if (!project.Billable)
			{
				return;
			}

			decimal contract = _random.Next(20, 500) * 1000m;
			var financials = new ProjectFinancials
			{
				Id = NewId(),
				ProjectId = project.Id,
				ContractValue = contract,
				UpdatedAt = DateTime.UtcNow,
				UpdatedBy = _admin.Id,
			};
			if (_random.NextDouble() < 0.3)
			{
				decimal change = _random.Next(-20, 60) * 1000m;
				financials.ChangeOrders.Add(new BudgetItem { Id = NewId(), Category = "Other", Description = "Change order 1", Amount = change });
				financials.ChangeOrder = change;
			}
			financials.TotalBudget = financials.ContractValue + financials.ChangeOrder;
			_financials.Add(financials);
		}

		// ---- Time -----------------------------------------------------------------

		// Every weekday up to today: a stat holiday, now and then a day of
		// vacation or sickness, otherwise a lunch break and 7.5h split over one
		// to three of their projects, in quarter hours.
		private void MakeTime()
		{
			var tasksOf = _tasks.GroupBy(t => t.ProjectId).ToDictionary(g => g.Key, g => g.ToList());
			var projectById = _projects.ToDictionary(p => p.Id);
			Project internalWork = _projects[^1];

			foreach (TeamMember person in _people)
			{
				List<(Project Project, DateOnly From, DateOnly? To)> theirs = _assignments
					.Where(a => a.TeamMemberId == person.Id && a.ProjectId != internalWork.Id)
					.Select(a => (projectById[a.ProjectId], a.StartDate, a.EndDate))
					.ToList();

				for (DateOnly day = _start; day <= _today; day = day.AddDays(1))
				{
					if (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday)
					{
						continue;
					}

					if (_holidays.Contains(day))
					{
						LogTimeOff(person, day, TimeOffTypes.StatHoliday, 8);
						continue;
					}
					double away = _random.NextDouble();
					if (away < 0.03)
					{
						LogTimeOff(person, day, TimeOffTypes.Vacation, 8);
						continue;
					}
					if (away < 0.045)
					{
						LogTimeOff(person, day, TimeOffTypes.SickLeave, 8);
						continue;
					}

					LogTimeOff(person, day, TimeOffTypes.LunchBreak, 0.5m);

					List<Project> active = theirs
						.Where(a => a.From <= day && (a.To == null || a.To >= day))
						.Select(a => a.Project)
						.ToList();
					if (active.Count == 0)
					{
						active.Add(internalWork);
					}

					List<Project> today = active.OrderBy(_ => _random.Next()).Take(_random.Next(1, Math.Min(3, active.Count) + 1)).ToList();
					foreach ((Project project, decimal hours) in Split(today, 30))
					{
						List<ProjectSubTask> options = tasksOf[project.Id];
						ProjectSubTask task = options[_random.Next(options.Count)];
						_entries.Add(Entry(person, project.Id, task, day, hours,
							_random.NextDouble() < 0.25 ? $"{task.Name} - {project.Name}" : null,
							project.Billable && task.Billable));
					}
				}
			}
		}

		// `quarters` quarter hours shared out over the projects, at least half
		// an hour each.
		private IEnumerable<(Project, decimal)> Split(List<Project> projects, int quarters)
		{
			int left = quarters;
			for (int i = 0; i < projects.Count; i++)
			{
				int remaining = projects.Count - i - 1;
				int share = remaining == 0 ? left : _random.Next(2, left - remaining * 2 + 1);
				left -= share;
				yield return (projects[i], share / 4m);
			}
		}

		private void LogTimeOff(TeamMember person, DateOnly day, string type, decimal hours)
		{
			if (_timeOff.TryGetValue(type, out ProjectSubTask task))
			{
				_entries.Add(Entry(person, task.ProjectId, task, day, hours, null, false));
			}
		}

		private TimeEntry Entry(TeamMember person, string projectId, ProjectSubTask task, DateOnly day, decimal hours, string notes, bool billable)
		{
			return new TimeEntry
			{
				Id = NewId(),
				ProjectId = projectId,
				ProjectTaskId = task.ProjectTaskId,
				ProjectSubTaskId = task.Id,
				TeamMemberId = person.Id,
				Date = day,
				Hours = hours,
				Notes = notes,
				Billable = billable,
				LastModified = day.ToDateTime(new TimeOnly(17, 0), DateTimeKind.Utc),
				ModifiedBy = person.Id,
			};
		}

		// ---- Expenses -------------------------------------------------------------

		// Up to two a month each, on a project they were on that day. The last
		// three weeks' are mostly still waiting for review.
		private void MakeExpenses()
		{
			Project internalWork = _projects[^1];
			var projectById = _projects.ToDictionary(p => p.Id);

			foreach (TeamMember person in _people)
			{
				var theirs = _assignments.Where(a => a.TeamMemberId == person.Id).ToList();
				for (DateOnly month = _start; month <= _today; month = month.AddMonths(1))
				{
					int count = _random.Next(0, 3);
					for (int n = 0; n < count; n++)
					{
						DateOnly day = month.AddDays(_random.Next(0, DateTime.DaysInMonth(month.Year, month.Month)));
						if (day > _today)
						{
							continue;
						}

						var onDay = theirs.Where(a => a.StartDate <= day && (a.EndDate == null || a.EndDate >= day) && a.ProjectId != internalWork.Id).ToList();
						Project project = onDay.Count > 0 ? projectById[onDay[_random.Next(onDay.Count)].ProjectId] : internalWork;
						var kind = ExpenseKinds[_random.Next(ExpenseKinds.Length)];

						var expense = new Expense
						{
							Id = NewId(),
							TeamMemberId = person.Id,
							ProjectId = project.Id,
							Date = day,
							Category = kind.Category,
							Notes = kind.Category == "Meals" ? "Lunch with the client to review the design" : (_random.NextDouble() < 0.4 ? $"{kind.Category} for {project.Name}" : ""),
							Amount = Math.Round(kind.Low + (decimal)_random.NextDouble() * (kind.High - kind.Low), 2),
							Currency = "CAD",
							Billable = project.Billable && _random.NextDouble() < 0.45,
							Reimbursable = _random.NextDouble() < 0.85,
							SubmittedAt = day.AddDays(_random.Next(0, 4)).ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc),
						};

						double roll = _random.NextDouble();
						bool recent = day > _today.AddDays(-21);
						string status = recent
							? (roll < 0.65 ? Expense.StatusSubmitted : Expense.StatusApproved)
							: (roll < 0.88 ? Expense.StatusApproved : roll < 0.92 ? Expense.StatusRejected : Expense.StatusSubmitted);
						expense.Status = status;
						if (status != Expense.StatusSubmitted)
						{
							expense.ReviewedBy = _admin.Id;
							expense.ReviewedAt = expense.SubmittedAt.AddDays(_random.Next(1, 8));
							if (status == Expense.StatusRejected)
							{
								expense.RejectionReason = "Please attach an itemised receipt.";
							}
						}
						_expenses.Add(expense);
					}
				}
			}
		}

		// ---- Invoices -------------------------------------------------------------

		// Each billable project's months with time, up to last month: a draft or
		// sent invoice for last month, older ones mostly paid, the odd one late
		// or cancelled - each with what an issued invoice keeps.
		private void MakeInvoices()
		{
			int.TryParse(Environment.GetEnvironmentVariable("LAST_INVOICE_NUMBER"), out int lastNumber);
			int number = Math.Max(lastNumber, 1000);

			var entriesByProjectMonth = _entries
				.GroupBy(e => (e.ProjectId, Month: MonthEnd(e.Date)))
				.ToDictionary(g => g.Key, g => g.ToList());
			var expensesByProjectMonth = _expenses
				.Where(e => e.Status == Expense.StatusApproved && e.Billable)
				.GroupBy(e => (e.ProjectId, Month: MonthEnd(e.Date)))
				.ToDictionary(g => g.Key, g => g.ToList());
			var clientById = _clients.ToDictionary(c => c.Id);
			var personById = _people.ToDictionary(p => p.Id);
			var financialsByProject = _financials.ToDictionary(f => f.ProjectId);
			DateOnly lastMonth = MonthEnd(_today.AddMonths(-1));

			var due = new List<(DateOnly Month, Project Project)>();
			foreach (Project project in _projects.Where(p => p.Billable))
			{
				for (DateOnly month = MonthEnd(project.StartDate ?? _start); month <= lastMonth; month = MonthEnd(month.AddDays(1)))
				{
					if (month >= MonthEnd(_start) && entriesByProjectMonth.ContainsKey((project.Id, month)))
					{
						due.Add((month, project));
					}
				}
			}

			foreach (var (month, project) in due.OrderBy(d => d.Month).ThenBy(d => d.Project.FundingCode))
			{
				List<TimeEntry> entries = entriesByProjectMonth[(project.Id, month)];
				List<Expense> billedExpenses = expensesByProjectMonth.TryGetValue((project.Id, month), out var found) ? found : new List<Expense>();
				DateOnly first = new DateOnly(month.Year, month.Month, 1);
				string po = string.IsNullOrWhiteSpace(project.PoNumber) ? "" : $" (PO: {project.PoNumber})";
				string period = $"{first.ToString("MMM d", CultureInfo.InvariantCulture)} – {month.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)}";

				var invoice = new Invoice
				{
					Id = NewId(),
					ProjectId = project.Id,
					Month = month,
					Number = ++number,
					CreatedAt = month.AddDays(2).ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc),
					CreatedBy = _admin.Id,
					InvoiceDate = month,
					Terms = 30,
					DueDate = month.AddDays(30),
					PoNumber = project.PoNumber,
					ProjectName = project.Name,
					Subject = $"{project.Name}{po} ({period})",
					BillFrom = "S.M.A. Consulting Ltd.",
					BillTo = project.CustomerId != null && clientById.TryGetValue(project.CustomerId, out Customer client) ? client.Name : "",
					Eft = "",
					TimeEntries = entries.Select(UserTimeEntry.From).ToList(),
					Taxes = new List<InvoiceTax> { new InvoiceTax { Id = NewLineId(), Label = "GST", Percent = Gst } },
				};

				invoice.Lines.AddRange(project.ProjectType == Project.TypeLumpSum
					? LumpSumLines(project, financialsByProject)
					: LabourLines(project, entries, personById, po, period));
				invoice.Lines.Add(new InvoiceLine
				{
					Id = NewLineId(),
					Kind = "flatDisbursement",
					ItemType = "Disbursement",
					Description = $"The flat rate disbursement of {FlatDisbursement}% is charged against the value of the monthly invoice.",
					Quantity = 1,
					Percent = FlatDisbursement,
				});
				foreach (Expense expense in billedExpenses.OrderBy(e => e.Date))
				{
					string notes = string.IsNullOrWhiteSpace(expense.Notes) ? "" : $" – {expense.Notes}";
					invoice.Lines.Add(new InvoiceLine
					{
						Id = NewLineId(),
						ItemType = "Expense",
						Description = $"{expense.Category}: {personById[expense.TeamMemberId].Name} ({expense.Date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)}){notes}",
						Amount = expense.Amount,
						ExpenseId = expense.Id,
						FromExpenses = true,
					});
				}

				SetStatus(invoice, month, lastMonth);
				if (invoice.Issued)
				{
					CopyBilled(invoice, project, billedExpenses);
					if (!invoice.Cancelled)
					{
						foreach (TimeEntry entry in entries)
						{
							entry.Invoiced = true;
							entry.InvoiceId = invoice.Id;
						}
						foreach (Expense expense in billedExpenses)
						{
							expense.Invoiced = true;
						}
					}
				}
				_invoices.Add(invoice);
			}
		}

		private IEnumerable<InvoiceLine> LumpSumLines(Project project, Dictionary<string, ProjectFinancials> financialsByProject)
		{
			int months = Math.Max(1, MonthsBetween(project.StartDate ?? _start, project.EndDate ?? _today.AddMonths(6)) + 1);
			decimal contract = financialsByProject.TryGetValue(project.Id, out var financials) ? financials.ContractValue : 100000m;
			yield return new InvoiceLine
			{
				Id = NewLineId(),
				ItemType = "Service",
				Description = "Lump sum fee",
				Amount = Math.Round(contract / months, 2),
			};
		}

		// One line per person and rate, as the Invoices page builds them.
		private IEnumerable<InvoiceLine> LabourLines(Project project, List<TimeEntry> entries,
			Dictionary<string, TeamMember> personById, string po, string period)
		{
			var roleOnProject = _assignments.Where(a => a.ProjectId == project.Id)
				.GroupBy(a => a.TeamMemberId)
				.ToDictionary(g => g.Key, g => g.First().ProjectRoleId);
			var ratesOnProject = _rates.Where(r => r.ProjectId == project.Id).ToList();

			return entries
				.Where(e => e.Billable && roleOnProject.ContainsKey(e.TeamMemberId))
				.Select(e => (Entry: e, Rate: ratesOnProject.FirstOrDefault(r =>
					r.ProjectRoleId == roleOnProject[e.TeamMemberId] && r.StartDate <= e.Date && (r.EndDate == null || r.EndDate >= e.Date))))
				.Where(x => x.Rate != null)
				.GroupBy(x => (x.Entry.TeamMemberId, x.Rate.Id))
				.Select(g => (Name: personById[g.Key.TeamMemberId].Name, Rate: g.First().Rate.HourlyRate, Hours: g.Sum(x => x.Entry.Hours), PersonId: g.Key.TeamMemberId))
				.OrderBy(g => g.Name)
				.Select(g => new InvoiceLine
				{
					Id = NewLineId(),
					ItemType = "Service",
					Description = $"{project.Name}{po}: {g.Name} ({period})",
					Quantity = g.Hours,
					Rate = g.Rate,
					CalculatedRate = g.Rate,
					TeamMemberId = g.PersonId,
					FromTimesheets = true,
				})
				.ToList();
		}

		private void SetStatus(Invoice invoice, DateOnly month, DateOnly lastMonth)
		{
			int monthsAgo = MonthsBetween(month, lastMonth);
			double roll = _random.NextDouble();

			bool draft = monthsAgo == 0 && roll < 0.4;
			if (draft)
			{
				return;
			}

			invoice.Issued = true;
			invoice.IssuedAt = month.AddDays(3).ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc);
			invoice.IssuedBy = _admin.Id;

			if (monthsAgo >= 3 && roll < 0.03)
			{
				invoice.Cancelled = true;
				invoice.CancelledAt = invoice.IssuedAt.Value.AddDays(5);
				invoice.CancelledBy = _admin.Id;
				return;
			}

			double paidShare = monthsAgo >= 3 ? 0.9 : monthsAgo == 2 ? 0.5 : 0;
			if (_random.NextDouble() < paidShare)
			{
				DateOnly paid = invoice.DueDate.Value.AddDays(_random.Next(-12, 25));
				invoice.Paid = true;
				invoice.PaymentDate = paid > _today ? _today : paid;
			}
		}

		// What an issued invoice keeps of its project and expenses.
		private void CopyBilled(Invoice invoice, Project project, List<Expense> billedExpenses)
		{
			invoice.BilledAssignments = _assignments.Where(a => a.ProjectId == project.Id).ToList();
			invoice.BilledRates = _rates.Where(r => r.ProjectId == project.Id).ToList();
			invoice.BilledTasks = _subprojects.Where(t => t.ProjectId == project.Id).ToList();
			invoice.BilledSubTasks = _tasks.Where(t => t.ProjectId == project.Id).ToList();
			invoice.BilledExpenses = billedExpenses.ToList();
		}

		// ---- Reimbursements -----------------------------------------------------

		// A batch on each 15th up to a month ago, paying back every approved,
		// reimbursable claim approved by then - leaving the latest ones waiting.
		private void MakeReimbursements()
		{
			var personById = _people.ToDictionary(p => p.Id);
			DateOnly batchDate = new DateOnly(_start.Year, _start.Month, 15).AddMonths(1);

			for (; batchDate <= _today.AddMonths(-1); batchDate = batchDate.AddMonths(1))
			{
				DateTime filedAt = batchDate.AddDays(1).ToDateTime(new TimeOnly(15, 0), DateTimeKind.Utc);
				List<Expense> paying = _expenses
					.Where(e => e.Status == Expense.StatusApproved && e.Reimbursable && !e.Reimbursed
						&& e.Date <= batchDate && e.ReviewedAt <= filedAt)
					.ToList();
				if (paying.Count == 0)
				{
					continue;
				}

				var batch = new ReimbursementBatch
				{
					Id = NewId(),
					BatchDate = batchDate,
					FiledAt = filedAt,
					FiledBy = _admin.Id,
				};
				foreach (var theirs in paying.GroupBy(e => e.TeamMemberId).OrderBy(g => personById[g.Key].Name))
				{
					batch.People.Add(new ReimbursementPerson
					{
						TeamMemberId = theirs.Key,
						Name = personById[theirs.Key].Name,
						Expenses = theirs.OrderBy(e => e.Date).ToList(),
						Subtotal = theirs.Sum(e => e.Amount),
					});
				}
				batch.Total = paying.Sum(e => e.Amount);
				batch.Count = paying.Count;

				foreach (Expense expense in paying)
				{
					expense.Reimbursed = true;
					expense.ReimbursementBatchId = batch.Id;
					expense.ReimbursedAt = filedAt;
				}
				_batches.Add(batch);
			}
		}

		// ---- Helpers ----------------------------------------------------------------

		private static void Insert<T>(List<T> documents) where T : GeneralModel
		{
			if (documents.Count == 0)
			{
				return;
			}
			var collection = Database.GetCollection<T>();
			for (int i = 0; i < documents.Count; i += InsertBatch)
			{
				collection.InsertMany(documents.Skip(i).Take(InsertBatch));
			}
		}

		private static string NewId() => ObjectId.GenerateNewId().ToString();

		private static string NewLineId() => Guid.NewGuid().ToString("N").Substring(0, 12);

		private static DateOnly MonthEnd(DateOnly day) =>
			new DateOnly(day.Year, day.Month, DateTime.DaysInMonth(day.Year, day.Month));

		private static int MonthsBetween(DateOnly from, DateOnly to) =>
			(to.Year - from.Year) * 12 + to.Month - from.Month;
	}
}
