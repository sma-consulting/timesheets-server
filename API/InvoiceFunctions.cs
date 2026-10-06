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
		private readonly ISecurityService _securityService;

		public InvoiceFunctions(
			IInvoiceRepo invoiceRepo,
			IProjectRepo projectRepo,
			ITimeEntryRepo timeEntryRepo,
			ISecurityService securityService)
		{
			_invoiceRepo = invoiceRepo;
			_projectRepo = projectRepo;
			_timeEntryRepo = timeEntryRepo;
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
			Invoice other = InvoiceFor(invoice.ProjectId, invoice.Month);
			if (other != null)
			{
				return Invalid(string.Format(
					"This project already has an invoice for {0}: {1}.", MonthName(invoice.Month), other.Number));
			}

			invoice.CreatedAt = DateTime.UtcNow;
			invoice.CreatedBy = _securityService.GetCurrentUserId();
			invoice.Issued = false;
			invoice.Cancelled = false;
			invoice.Approvals = new List<InvoiceApproval>();
			invoice.TimeEntries = TimeEntriesFor(invoice);

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

			string invalid = Validate(invoice);
			if (invalid != null)
			{
				return Invalid(invalid);
			}

			invoice.Number = stored.Number;
			invoice.CreatedAt = stored.CreatedAt;
			invoice.CreatedBy = stored.CreatedBy;
			invoice.Issued = stored.Issued;
			invoice.Cancelled = stored.Cancelled;
			invoice.Approvals = stored.Approvals ?? new List<InvoiceApproval>();
			// A draft bills the month's hours as they are now.
			invoice.TimeEntries = TimeEntriesFor(invoice);

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
			if (invoice.InvoiceDate.HasValue && invoice.DueDate.HasValue && invoice.DueDate < invoice.InvoiceDate)
			{
				return "The due date can't be before the invoice date.";
			}

			invoice.Lines ??= new List<InvoiceLine>();
			invoice.Taxes ??= new List<InvoiceTax>();
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
