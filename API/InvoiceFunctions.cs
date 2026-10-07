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
	// Invoices, for admins only. The page sends the invoice as edited; the
	// server alone sets its number, who created it and when, its status, and the
	// time entries it bills, whatever the request says.
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

			if (!_projectRepo.GetAll().Any(p => p.Id == invoice.ProjectId))
			{
				return Invalid("That project no longer exists.");
			}

			invoice.Month = LastDayOf(invoice.Month);
			invalid = SetDates(invoice);
			if (invalid != null)
			{
				return Invalid(invalid);
			}

			Invoice other = InvoiceFor(invoice.ProjectId, invoice.Month);
			if (other != null)
			{
				return Invalid(string.Format(
					"This project already has an invoice for {0}: {1}.", MonthName(invoice.Month), other.Number));
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


		[Allow(Role.Admin)]
		[Function("GetInvoice")]
		public async Task<IActionResult> RunGetInvoice(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "invoice/{id}/")] HttpRequest req, string id)
		{
			return Ok(
				() => _invoiceRepo.Get(id),
				(i) => new
				{
					invoice = i
				});
		}


		[Allow(Role.Admin)]
		[Function("GetAllInvoice")]
		public async Task<IActionResult> RunGetAllInvoice(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "invoice/")] HttpRequest req)
		{
			return Ok(
				() => _invoiceRepo.GetAll(),
				(i) => new
				{
					invoiceList = i
				});
		}


		// Saves changes to a draft. Its project and month, and everything the
		// server sets, stay as they were.
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

			return Ok(
				() =>
				{
					Invoice issued = _invoiceRepo.Update(stored).Item2;
					MarkExpensesInvoiced(issued, true);
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

		// Marks the expenses an invoice bills as invoiced (when it's issued) or
		// frees them again (when it's cancelled). Ones deleted since are skipped.
		private void MarkExpensesInvoiced(Invoice invoice, bool invoiced)
		{
			HashSet<string> ids = (invoice.Lines ?? new List<InvoiceLine>())
				.Select(l => l.ExpenseId)
				.Where(e => !string.IsNullOrWhiteSpace(e))
				.ToHashSet();
			if (ids.Count == 0)
			{
				return;
			}

			foreach (Expense expense in _expenseRepo.GetAll().Where(e => ids.Contains(e.Id)))
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
			if (string.IsNullOrWhiteSpace(invoice.ProjectId))
			{
				return "Choose the project the invoice is for.";
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

		// The project's invoice for the month, if it has one. Cancelled ones don't
		// count: cancelling frees the month for a new invoice.
		private Invoice InvoiceFor(string projectId, DateOnly month)
		{
			return _invoiceRepo.GetAll()
				.FirstOrDefault(i => !i.Cancelled && i.ProjectId == projectId && i.Month == month);
		}

		// The project's time entries in the invoice's month.
		private List<UserTimeEntry> TimeEntriesFor(Invoice invoice)
		{
			DateOnly first = new DateOnly(invoice.Month.Year, invoice.Month.Month, 1);
			return _timeEntryRepo.GetAll()
				.Where(e => e.ProjectId == invoice.ProjectId && e.Date >= first && e.Date <= invoice.Month)
				.OrderBy(e => e.Date)
				.Select(UserTimeEntry.From)
				.ToList();
		}

		// Copies what priced and grouped the invoice's hours, as it is at issue:
		// the project's assignments, billing rates, tasks and sub-tasks.
		private void CopyBilledProject(Invoice invoice)
		{
			string projectId = invoice.ProjectId;
			invoice.BilledAssignments = _assignmentRepo.GetByProject(projectId);
			invoice.BilledRates = _rateRepo.GetByProject(projectId);
			invoice.BilledTasks = _taskRepo.GetAll().Where(t => t.ProjectId == projectId).ToList();

			// A sub-task's ProjectId is missing on older records, so its task
			// decides too.
			HashSet<string> taskIds = invoice.BilledTasks.Select(t => t.Id).ToHashSet();
			invoice.BilledSubTasks = _subTaskRepo.GetAll()
				.Where(s => s.ProjectId == projectId || taskIds.Contains(s.ProjectTaskId))
				.ToList();
		}

		// A draft has none: its report is built from the project as it is now.
		private static void ClearBilledProject(Invoice invoice)
		{
			invoice.BilledAssignments = new List<ProjectAssignment>();
			invoice.BilledRates = new List<ProjectBillingRate>();
			invoice.BilledTasks = new List<ProjectTask>();
			invoice.BilledSubTasks = new List<ProjectSubTask>();
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
