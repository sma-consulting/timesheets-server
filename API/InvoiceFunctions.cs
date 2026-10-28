using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace sma.plan
{
	// Invoices, made and changed by admins. A project manager may look at the
	// issued ones billing a project they manage, but not change them. The page
	// sends the invoice as edited; the server alone sets its number, who
	// created it and when, its status, and the time entries it bills, whatever
	// the request says.
	internal class InvoiceFunctions : RequestHandler
	{
		private readonly IInvoiceRepo _invoiceRepo;
		private readonly IProjectRepo _projectRepo;
		private readonly ITimeEntryRepo _timeEntryRepo;
		private readonly IExpenseRepo _expenseRepo;
		private readonly IProjectAssignmentRepo _assignmentRepo;
		private readonly IProjectBillingRateRepo _rateRepo;
		private readonly IProjectTaskRepo _taskRepo;
		private readonly IProjectSubTaskRepo _subTaskRepo;
		private readonly IProjectService _projectService;
		private readonly ISecurityService _securityService;

		public InvoiceFunctions(
			IInvoiceRepo invoiceRepo,
			IProjectRepo projectRepo,
			ITimeEntryRepo timeEntryRepo,
			IExpenseRepo expenseRepo,
			IProjectAssignmentRepo assignmentRepo,
			IProjectBillingRateRepo rateRepo,
			IProjectTaskRepo taskRepo,
			IProjectSubTaskRepo subTaskRepo,
			IProjectService projectService,
			ISecurityService securityService)
		{
			_invoiceRepo = invoiceRepo;
			_projectRepo = projectRepo;
			_timeEntryRepo = timeEntryRepo;
			_expenseRepo = expenseRepo;
			_assignmentRepo = assignmentRepo;
			_rateRepo = rateRepo;
			_taskRepo = taskRepo;
			_subTaskRepo = subTaskRepo;
			_projectService = projectService;
			_securityService = securityService;
		}

		[Allow(Role.Admin)]
		[Function("CreateInvoice")]
		public async Task<IActionResult> RunCreateInvoice(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "invoice/")] HttpRequest req)
		{
			Invoice invoice = JsonConvert.DeserializeObject<Invoice>(
				await new StreamReader(req.Body).ReadToEndAsync());

			string invalid = Validate(invoice);
			if (invalid != null)
			{
				return Invalid(invalid);
			}
			invalid = ResolveProjects(invoice, out List<ProjectTask> subprojects);
			if (invalid != null)
			{
				return Invalid(invalid);
			}

			invoice.Month = LastDayOf(invoice.Month);
			invalid = SetDates(invoice) ?? AlreadyInvoiced(invoice, subprojects);
			if (invalid != null)
			{
				return Invalid(invalid);
			}

			invoice.CreatedAt = DateTime.UtcNow;
			invoice.CreatedBy = _securityService.GetCurrentUserId();
			// Every invoice starts as a draft.
			invoice.Issued = false;
			invoice.IssuedAt = null;
			invoice.IssuedBy = null;
			invoice.Cancelled = false;
			invoice.CancelledAt = null;
			invoice.CancelledBy = null;
			invoice.Paid = false;
			invoice.PaymentDate = null;
			invoice.Approvals = new List<InvoiceApproval>();
			invoice.TimeEntries = TimeEntriesFor(invoice);
			ClearBilledProject(invoice);

			return Ok(
				() => _invoiceRepo.CreateNumbered(invoice),
				(i) => new
				{
					invoice = i
				});
		}


		// Any invoice for an admin; for a project manager, an issued one billing
		// a project they manage - the whole invoice, as the client got it.
		[Allow(Role.SignedIn)]
		[Function("GetInvoice")]
		public async Task<IActionResult> RunGetInvoice(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "invoice/{id}/")] HttpRequest req, string id)
		{
			if (_securityService.IsCurrentUserAdmin())
			{
				return Ok(
					() => _invoiceRepo.Get(id),
					(i) => new
					{
						invoice = i
					});
			}

			Invoice stored = _invoiceRepo.Find(id);
			HashSet<string> managed = _projectService.ManagedProjectIds();
			if (stored == null || !stored.Issued || !stored.AllProjectIds().Any(managed.Contains))
			{
				return Forbidden("That invoice isn't on a project you manage.");
			}

			return Ok(
				() => WithBilledCopies(new List<Invoice> { stored })[0],
				(i) => new
				{
					invoice = i
				});
		}


		// Invoices for lists, as summaries: everything but the copies kept for
		// their reports, which come with the invoice itself (GetInvoice).
		// Narrowed by ?projectId=, ?issued=true, and ?from=&to= (yyyy-MM-dd) on
		// the month billed - with ?includeOpen=true adding every invoice still
		// open (not paid, not cancelled) whatever its month, so a range never
		// hides what's still owed. Any invoice for an admin. A project manager
		// gets the issued ones - sent, paid or cancelled, never drafts - on the
		// projects they manage; anyone else, none.
		[Allow(Role.SignedIn)]
		[Function("GetAllInvoice")]
		public async Task<IActionResult> RunGetAllInvoice(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "invoice/")] HttpRequest req)
		{
			ApiError badDates = ReadDateRange(req, out DateOnly? from, out DateOnly? to);
			if (badDates != null)
			{
				return Invalid(badDates);
			}
			string projectId = QueryValue(req, "projectId");
			bool issuedOnly = string.Equals(QueryValue(req, "issued"), "true", StringComparison.OrdinalIgnoreCase);
			bool includeOpen = string.Equals(QueryValue(req, "includeOpen"), "true", StringComparison.OrdinalIgnoreCase);

			if (_securityService.IsCurrentUserAdmin())
			{
				return Ok(
					() => _invoiceRepo.GetSummaries(projectId == null ? null : new[] { projectId }, issuedOnly, from, to, includeOpen),
					(i) => new
					{
						invoiceList = i
					});
			}

			HashSet<string> managed = _projectService.ManagedProjectIds();
			IEnumerable<string> theirs = projectId == null ? managed : managed.Where(id => id == projectId);
			return Ok(
				() => _invoiceRepo.GetSummaries(theirs, issuedOnly: true, from, to, includeOpen),
				(i) => new
				{
					invoiceList = i
				});
		}


		// Saves changes to a draft. What it bills and its month, and everything
		// the server sets, stay as they were.
		[Allow(Role.Admin)]
		[Function("UpdateInvoice")]
		public async Task<IActionResult> RunUpdateInvoice(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "invoice/{id}/update/")] HttpRequest req, string id)
		{
			Invoice invoice = JsonConvert.DeserializeObject<Invoice>(
				await new StreamReader(req.Body).ReadToEndAsync());

			Invoice stored = _invoiceRepo.Find(id);
			if (stored == null)
			{
				return Invalid("That invoice no longer exists.");
			}
			if (stored.Issued || stored.Cancelled)
			{
				return Invalid("An issued or cancelled invoice can't be changed.");
			}

			if (invoice == null)
			{
				return Invalid("The invoice is missing.");
			}
			invoice.Id = id;
			invoice.ProjectId = stored.ProjectId;
			invoice.ProjectIds = stored.ProjectIds ?? new List<string>();
			invoice.ProjectTaskIds = stored.ProjectTaskIds ?? new List<string>();
			invoice.Month = stored.Month;

			string invalid = Validate(invoice) ?? SetDates(invoice);
			if (invalid != null)
			{
				return Invalid(invalid);
			}

			invoice.Number = stored.Number;
			invoice.CreatedAt = stored.CreatedAt;
			invoice.CreatedBy = stored.CreatedBy;
			invoice.Issued = stored.Issued;
			invoice.IssuedAt = stored.IssuedAt;
			invoice.IssuedBy = stored.IssuedBy;
			invoice.Cancelled = stored.Cancelled;
			invoice.CancelledAt = stored.CancelledAt;
			invoice.CancelledBy = stored.CancelledBy;
			invoice.Paid = stored.Paid;
			invoice.PaymentDate = stored.PaymentDate;
			invoice.Approvals = stored.Approvals ?? new List<InvoiceApproval>();
			// A draft bills the month's hours as they are now.
			invoice.TimeEntries = TimeEntriesFor(invoice);
			ClearBilledProject(invoice);

			return Ok(
				() => _invoiceRepo.Update(invoice).Item2,
				(i) => new
				{
					invoice = i
				});
		}


		// Only a draft can go. An issued invoice was sent to the customer, so it
		// stays on record (cancelled, if need be) with its number.
		[Allow(Role.Admin)]
		[Function("DeleteInvoice")]
		public async Task<IActionResult> RunDeleteInvoice(
			[HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "invoice/{id}/")] HttpRequest req, string id)
		{
			Invoice stored = _invoiceRepo.Find(id);
			if (stored == null)
			{
				return Invalid("That invoice no longer exists.");
			}
			if (stored.Issued || stored.Cancelled)
			{
				return Invalid("An issued or cancelled invoice stays on record and can't be deleted.");
			}

			return Ok(
				() => _invoiceRepo.Delete(id),
				(i) => new
				{
					deletedInvoice = i
				});
		}


		// Sends a saved draft to the customer. From then on it's locked, the hours
		// it bills are frozen as they are now, and its expenses are marked
		// invoiced so no later invoice bills them again.
		[Allow(Role.Admin)]
		[Function("IssueInvoice")]
		public async Task<IActionResult> RunIssueInvoice(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "invoice/{id}/issue/")] HttpRequest req, string id)
		{
			Invoice stored = _invoiceRepo.Find(id);
			if (stored == null)
			{
				return Invalid("That invoice no longer exists.");
			}
			if (stored.Cancelled)
			{
				return Invalid("A cancelled invoice can't be issued.");
			}
			if (stored.Issued)
			{
				return Invalid("That invoice has already been issued.");
			}

			stored.Issued = true;
			stored.IssuedAt = DateTime.UtcNow;
			stored.IssuedBy = _securityService.GetCurrentUserId();
			stored.TimeEntries = TimeEntriesFor(stored);
			CopyBilledProject(stored);
			CopyBilledExpenses(stored, _expenseRepo.GetByIds(ExpenseIdsOf(stored)));

			return Ok(
				() =>
				{
					Invoice issued = _invoiceRepo.Update(stored).Item2;
					MarkExpensesInvoiced(issued, true);
					_timeEntryRepo.MarkInvoiced(
						(issued.TimeEntries ?? new List<UserTimeEntry>()).Select(e => e.TimeEntryId),
						issued.Id);
					return issued;
				},
				(i) => new
				{
					invoice = i
				});
		}


		// Withdraws an issued invoice. It stays on record with its number; its
		// expenses are free to bill again, and its month is free for a new
		// invoice. A paid invoice has to be marked unpaid first.
		[Allow(Role.Admin)]
		[Function("CancelInvoice")]
		public async Task<IActionResult> RunCancelInvoice(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "invoice/{id}/cancel/")] HttpRequest req, string id)
		{
			Invoice stored = _invoiceRepo.Find(id);
			if (stored == null)
			{
				return Invalid("That invoice no longer exists.");
			}
			if (!stored.Issued)
			{
				return Invalid("Only an issued invoice can be cancelled. A draft can simply be deleted.");
			}
			if (stored.Cancelled)
			{
				return Invalid("That invoice is already cancelled.");
			}
			if (stored.Paid)
			{
				return Invalid("A paid invoice can't be cancelled. Mark it unpaid first.");
			}

			stored.Cancelled = true;
			stored.CancelledAt = DateTime.UtcNow;
			stored.CancelledBy = _securityService.GetCurrentUserId();

			return Ok(
				() =>
				{
					Invoice cancelled = _invoiceRepo.Update(stored).Item2;
					MarkExpensesInvoiced(cancelled, false);
					_timeEntryRepo.ClearInvoiced(cancelled.Id);
					return cancelled;
				},
				(i) => new
				{
					invoice = i
				});
		}


		// Records the customer's payment on an issued invoice, or takes it back.
		[Allow(Role.Admin)]
		[Function("SetInvoicePayment")]
		public async Task<IActionResult> RunSetInvoicePayment(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "invoice/{id}/payment/")] HttpRequest req, string id)
		{
			PaymentRequest payment = JsonConvert.DeserializeObject<PaymentRequest>(
				await new StreamReader(req.Body).ReadToEndAsync());
			if (payment == null)
			{
				return Invalid("The payment is missing.");
			}

			Invoice stored = _invoiceRepo.Find(id);
			if (stored == null)
			{
				return Invalid("That invoice no longer exists.");
			}
			if (!stored.Issued || stored.Cancelled)
			{
				return Invalid("A payment can only be recorded on an issued invoice.");
			}
			if (payment.Paid && !payment.PaymentDate.HasValue)
			{
				return Invalid("Enter the date the payment was received.");
			}
			if (payment.Paid && payment.PaymentDate > DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1))
			{
				return Invalid("The payment date can't be in the future.");
			}

			stored.Paid = payment.Paid;
			stored.PaymentDate = payment.Paid ? payment.PaymentDate : null;

			return Ok(
				() => _invoiceRepo.Update(stored).Item2,
				(i) => new
				{
					invoice = i
				});
		}


		private class PaymentRequest
		{
			public bool Paid { get; set; }

			public DateOnly? PaymentDate { get; set; }
		}

		// The ids of the expenses an invoice bills.
		private static HashSet<string> ExpenseIdsOf(Invoice invoice)
		{
			return (invoice.Lines ?? new List<InvoiceLine>())
				.Select(l => l.ExpenseId)
				.Where(e => !string.IsNullOrWhiteSpace(e))
				.ToHashSet();
		}

		// Marks the expenses an invoice bills as invoiced (when it's issued) or
		// frees them again (when it's cancelled). Ones deleted since are skipped.
		private void MarkExpensesInvoiced(Invoice invoice, bool invoiced)
		{
			HashSet<string> ids = ExpenseIdsOf(invoice);
			if (ids.Count == 0)
			{
				return;
			}

			foreach (Expense expense in _expenseRepo.GetByIds(ids))
			{
				if (expense.Invoiced != invoiced)
				{
					expense.Invoiced = invoiced;
					_expenseRepo.Update(expense);
				}
			}
		}

		private static string Validate(Invoice invoice)
		{
			if (invoice == null)
			{
				return "The invoice is missing.";
			}
			if (invoice.Month == default)
			{
				return "Choose the month the invoice is for.";
			}
			if (invoice.Terms < 0)
			{
				return "Payment terms can't be negative.";
			}

			invoice.Lines ??= new List<InvoiceLine>();
			invoice.Taxes ??= new List<InvoiceTax>();
			return null;
		}

		// An invoice is always dated the last day of the month it bills. With
		// terms (Net 30) its due date is that many days later; a due date set by
		// hand (no terms) is kept as sent, but can't come before the invoice date.
		private static string SetDates(Invoice invoice)
		{
			invoice.InvoiceDate = invoice.Month;
			if (invoice.Terms.HasValue)
			{
				invoice.DueDate = invoice.Month.AddDays(invoice.Terms.Value);
			}
			if (invoice.DueDate.HasValue && invoice.DueDate < invoice.InvoiceDate)
			{
				return "The due date can't be before the invoice date.";
			}
			return null;
		}

		// What a new invoice bills: the subprojects chosen - all of ProjectId's
		// when none are - and from them its projects, which must exist and, more
		// than one, all be for the same client. The page's project stays first.
		// Returns why not, or null.
		private string ResolveProjects(Invoice invoice, out List<ProjectTask> subprojects)
		{
			List<string> taskIds = (invoice.ProjectTaskIds ?? new List<string>())
				.Where(id => !string.IsNullOrWhiteSpace(id))
				.Distinct()
				.ToList();
			if (taskIds.Count > 0)
			{
				subprojects = _taskRepo.GetByIds(taskIds);
				if (subprojects.Count != taskIds.Count)
				{
					return "One of those subprojects no longer exists.";
				}
			}
			else if (!string.IsNullOrWhiteSpace(invoice.ProjectId))
			{
				subprojects = _taskRepo.GetByProjects(new[] { invoice.ProjectId });
				taskIds = subprojects.Select(t => t.Id).ToList();
			}
			else
			{
				subprojects = new List<ProjectTask>();
				return "Choose what the invoice is for.";
			}

			List<string> projectIds = (subprojects.Count > 0 ? subprojects.Select(t => t.ProjectId) : new[] { invoice.ProjectId })
				.Where(id => !string.IsNullOrWhiteSpace(id))
				.Distinct()
				.OrderBy(id => id == invoice.ProjectId ? 0 : 1)
				.ToList();
			List<Project> projects = _projectRepo.GetByIds(projectIds);
			if (projectIds.Count == 0 || projects.Count != projectIds.Count)
			{
				return "That project no longer exists.";
			}
			if (projects.Count > 1
				&& (projects.Any(p => string.IsNullOrWhiteSpace(p.CustomerId))
					|| projects.Select(p => p.CustomerId).Distinct().Count() > 1))
			{
				return "The projects on one invoice must all be for the same client.";
			}

			invoice.ProjectTaskIds = taskIds;
			invoice.ProjectIds = projectIds;
			invoice.ProjectId = projectIds[0];
			return null;
		}

		// A subproject is on one invoice a month. Cancelled ones don't count:
		// cancelling frees the month for a new invoice. Returns why not, or null.
		private string AlreadyInvoiced(Invoice invoice, List<ProjectTask> subprojects)
		{
			Invoice other = _invoiceRepo
				.ActiveCovering(invoice.Month, invoice.ProjectIds, invoice.ProjectTaskIds, null)
				.FirstOrDefault();
			if (other == null)
			{
				return null;
			}

			// An older invoice bills all of its project.
			bool older = other.ProjectTaskIds == null || other.ProjectTaskIds.Count == 0;
			ProjectTask taken = subprojects.FirstOrDefault(t =>
				older ? t.ProjectId == other.ProjectId : other.ProjectTaskIds.Contains(t.Id));
			return taken == null
				? string.Format("This project already has an invoice for {0}: {1}.", MonthName(invoice.Month), other.Number)
				: string.Format("{0} is already on invoice {1} for {2}.", taken.Name, other.Number, MonthName(invoice.Month));
		}

		// The time an invoice bills: its subprojects' entries in its month - on
		// an older invoice, all of its project's.
		private List<UserTimeEntry> TimeEntriesFor(Invoice invoice)
		{
			DateOnly first = new DateOnly(invoice.Month.Year, invoice.Month.Month, 1);
			List<TimeEntry> entries = invoice.ProjectTaskIds != null && invoice.ProjectTaskIds.Count > 0
				? _timeEntryRepo.GetOnTasks(invoice.ProjectTaskIds, first, invoice.Month)
				: _timeEntryRepo.GetFiltered(null, invoice.ProjectId, first, invoice.Month, null, null);
			return entries
				.OrderBy(e => e.Date)
				.Select(UserTimeEntry.From)
				.ToList();
		}

		// Copies what priced and grouped the invoice's hours, as it is at issue:
		// its projects' assignments, billing rates, tasks and sub-tasks.
		private void CopyBilledProject(Invoice invoice)
		{
			List<string> projectIds = invoice.AllProjectIds();
			invoice.BilledAssignments = _assignmentRepo.GetByProjects(projectIds);
			invoice.BilledRates = _rateRepo.GetByProjects(projectIds);
			invoice.BilledTasks = _taskRepo.GetByProjects(projectIds);

			// A sub-task's ProjectId is missing on older records, so its task
			// decides too.
			HashSet<string> taskIds = invoice.BilledTasks.Select(t => t.Id).ToHashSet();
			invoice.BilledSubTasks = _subTaskRepo.GetInProjects(projectIds, taskIds);
		}

		// Copies the expenses the invoice bills, as they are at issue.
		private static void CopyBilledExpenses(Invoice invoice, IEnumerable<Expense> expenses)
		{
			HashSet<string> ids = ExpenseIdsOf(invoice);
			invoice.BilledExpenses = ids.Count == 0
				? new List<Expense>()
				: expenses.Where(e => ids.Contains(e.Id)).ToList();
		}

		// A draft has none: its report is built from the project as it is now.
		private static void ClearBilledProject(Invoice invoice)
		{
			invoice.BilledAssignments = new List<ProjectAssignment>();
			invoice.BilledRates = new List<ProjectBillingRate>();
			invoice.BilledTasks = new List<ProjectTask>();
			invoice.BilledSubTasks = new List<ProjectSubTask>();
			invoice.BilledExpenses = new List<Expense>();
		}

		// For a project manager, who can't read a project's rates or other
		// people's expenses: invoices issued before the copies were kept get the
		// missing ones from the project as it is now - what an admin's page
		// falls back on too. Only on the way out; nothing is saved.
		private List<Invoice> WithBilledCopies(List<Invoice> invoices)
		{
			List<Expense> expenses = null;
			foreach (Invoice invoice in invoices)
			{
				if ((invoice.BilledAssignments?.Count ?? 0) == 0 && (invoice.BilledRates?.Count ?? 0) == 0)
				{
					CopyBilledProject(invoice);
				}
				if ((invoice.BilledExpenses?.Count ?? 0) == 0 && ExpenseIdsOf(invoice).Count > 0)
				{
					// Just the expenses these invoices bill, in one read.
					expenses ??= _expenseRepo.GetByIds(invoices.SelectMany(i => ExpenseIdsOf(i)));
					CopyBilledExpenses(invoice, expenses);
				}
			}
			return invoices;
		}

		// An invoice's month is kept as its last day, whichever day was sent.
		private static DateOnly LastDayOf(DateOnly date)
		{
			return new DateOnly(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));
		}

		private static string MonthName(DateOnly month)
		{
			return month.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
		}
	}
}
