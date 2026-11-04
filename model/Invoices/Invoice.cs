using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace sma.plan
{
	// An invoice for a calendar month, or another period, to one client: the
	// subprojects it bills, from one or more of that client's projects. Built on
	// the Invoices page from the period's hours and expenses, then edited there;
	// what's saved is the invoice as edited, so its figures never move once it's sent,
	// whatever happens to the time entries or rates later.
	internal class Invoice : GeneralModel
	{
		// The first of its projects.
		[BsonRepresentation(BsonType.ObjectId)]
		public string ProjectId { get; set; }

		// Every project it bills, ProjectId first, and the subprojects it bills
		// from them. Both empty on invoices made before one could bill several:
		// those bill all of ProjectId (see AllProjectIds).
		public List<string> ProjectIds { get; set; } = new List<string>();

		public List<string> ProjectTaskIds { get; set; } = new List<string>();

		// The month billed, kept as its last day: 2026-09-30 is September 2026.
		// For another period, the month it ends in - where lists, exports and
		// Financials put it.
		public DateOnly Month { get; set; }

		// The days billed when they aren't just Month: part of a month, or
		// several. Both empty for a whole month, as on every older invoice (see
		// From and To). A subproject is on one invoice for any day, not counting
		// cancelled ones.
		[BsonIgnoreIfNull, JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public DateOnly? PeriodStart { get; set; }

		[BsonIgnoreIfNull, JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public DateOnly? PeriodEnd { get; set; }

		// Given by the server when the invoice is first saved: one past the
		// highest so far, carrying on from SMA's own numbering (Access's InvoiceID).
		public int Number { get; set; }

		public DateTime CreatedAt { get; set; }

		// The User who created it.
		[BsonRepresentation(BsonType.ObjectId)]
		public string CreatedBy { get; set; }

		// Sent to the customer. From then on it can't be changed or deleted, and
		// the expenses it bills are marked invoiced.
		public bool Issued { get; set; }

		public DateTime? IssuedAt { get; set; }

		// The User who issued it.
		[BsonRepresentation(BsonType.ObjectId)]
		public string IssuedBy { get; set; }

		// Withdrawn after it was issued. Kept on record with its number, its
		// expenses are free to bill again, and its month is free for a new
		// invoice.
		public bool Cancelled { get; set; }

		public DateTime? CancelledAt { get; set; }

		[BsonRepresentation(BsonType.ObjectId)]
		public string CancelledBy { get; set; }

		// Payment received, recorded on an issued invoice.
		public bool Paid { get; set; }

		public DateOnly? PaymentDate { get; set; }

		// The month's time entries on the project, copied in on each save while
		// the invoice is a draft - what it bills, as it was when billed. Once
		// issued, these entries can't be changed until the invoice is cancelled.
		public List<UserTimeEntry> TimeEntries { get; set; } = new List<UserTimeEntry>();

		// What priced and grouped those hours, copied when the invoice is issued:
		// the project's assignments (each person's role), billing rates, tasks
		// and sub-tasks. With them the invoice's hours report shows exactly what
		// was billed, whatever changes on the project later. Empty on a draft.
		public List<ProjectAssignment> BilledAssignments { get; set; } = new List<ProjectAssignment>();

		public List<ProjectBillingRate> BilledRates { get; set; } = new List<ProjectBillingRate>();

		public List<ProjectTask> BilledTasks { get; set; } = new List<ProjectTask>();

		public List<ProjectSubTask> BilledSubTasks { get; set; } = new List<ProjectSubTask>();

		// The expenses it bills, copied when it's issued. The expense report and
		// its receipts come from these, so a project manager - who can't read
		// other people's expenses - sees the invoice whole. Empty on a draft.
		public List<Expense> BilledExpenses { get; set; } = new List<Expense>();

		// The document itself, as on the page and the PDF.

		// Always the last day of Month; the server sets it on every save.
		public DateOnly? InvoiceDate { get; set; }

		// Days after the invoice date it's due (Net 30); null when the due date
		// was set by hand.
		public int? Terms { get; set; }

		public DateOnly? DueDate { get; set; }

		public string PoNumber { get; set; }

		public string ProjectName { get; set; }

		public string Subject { get; set; }

		public string BillFrom { get; set; }

		public string BillTo { get; set; }

		public string Notes { get; set; }

		public string Eft { get; set; }

		public List<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();

		// How its hours are listed: "costCode", a line per subproject (as the
		// spec and SMA's Harvest invoices have it), or "person", a line per
		// person and rate. Chosen on the page; empty on older invoices.
		[BsonIgnoreIfNull, JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string LinesBy { get; set; }

		public List<InvoiceTax> Taxes { get; set; } = new List<InvoiceTax>();

		// What was done in the period, a note per project it bills, for that
		// project's progress report. Written on the Reports page, and kept when
		// the invoice is saved or issued - it can change after issuing, as the
		// report is often finished then.
		public List<InvoiceWorkNote> WorkCompleted { get; set; } = new List<InvoiceWorkNote>();

		// On a draft: the project managers asked to approve it, one each with the
		// projects of theirs it bills. It's approved once every one has. Empty
		// until an admin asks; set only by the approval endpoints, never by a save.
		public List<InvoiceApproval> Approvals { get; set; } = new List<InvoiceApproval>();

		// ProjectIds, or ProjectId alone on an older invoice.
		public List<string> AllProjectIds() =>
			ProjectIds != null && ProjectIds.Count > 0 ? ProjectIds : new List<string> { ProjectId };

		// The first and last day billed: the period, or the whole of Month.
		public DateOnly From() => PeriodStart ?? new DateOnly(Month.Year, Month.Month, 1);

		public DateOnly To() => PeriodEnd ?? Month;
	}

	// A row of the invoice table: a person's hours at their rate, a cost code's
	// hours at its people's rates, the flat disbursement, an expense at cost, a
	// lump-sum fee or a line added by hand.
	// Which fields a row has depends on its kind, so empty ones are left out
	// rather than stored or sent as null - the page tells the kinds apart by
	// what's there.
	internal class InvoiceLine
	{
		// The page's own key for the row.
		public string Id { get; set; }

		// The project the row bills. Empty on older invoices, whose rows are all
		// the invoice's ProjectId.
		[BsonIgnoreIfNull, JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string ProjectId { get; set; }

		[BsonIgnoreIfNull, JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string Kind { get; set; }

		[BsonIgnoreIfNull, JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string ItemType { get; set; }

		[BsonIgnoreIfNull, JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string Description { get; set; }

		[BsonIgnoreIfNull, JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public decimal? Quantity { get; set; }

		[BsonIgnoreIfNull, JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public decimal? Rate { get; set; }

		// The rate the timesheets priced it at, before any change on the invoice.
		[BsonIgnoreIfNull, JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public decimal? CalculatedRate { get; set; }

		// Set instead of quantity × rate on a lump-sum fee, an expense, or a cost
		// code's hours (its people bill at different rates).
		[BsonIgnoreIfNull, JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public decimal? Amount { get; set; }

		// A cost code's amount as the timesheets priced it, before any change on
		// the invoice.
		[BsonIgnoreIfNull, JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public decimal? CalculatedAmount { get; set; }

		// The subproject (cost code) a cost code row bills.
		[BsonIgnoreIfNull, JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string ProjectTaskId { get; set; }

		// The flat disbursement's percentage of the other lines.
		[BsonIgnoreIfNull, JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public decimal? Percent { get; set; }

		[BsonIgnoreIfNull, JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string TeamMemberId { get; set; }

		[BsonIgnoreIfNull, JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string ExpenseId { get; set; }

		[BsonIgnoreIfNull, JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public bool? FromTimesheets { get; set; }

		[BsonIgnoreIfNull, JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public bool? FromExpenses { get; set; }
	}

	internal class InvoiceTax
	{
		public string Id { get; set; }

		public string Label { get; set; }

		public decimal? Percent { get; set; }
	}

	// One project manager's say on a draft: waiting to be asked again, approved,
	// or changes asked for - with what to change.
	internal class InvoiceApproval
	{
		public const string StatusRequested = "Requested";
		public const string StatusApproved = "Approved";
		public const string StatusChangesRequested = "ChangesRequested";

		[BsonRepresentation(BsonType.ObjectId)]
		public string ProjectManagerId { get; set; }

		// The projects of theirs on the invoice.
		public List<string> ProjectIds { get; set; } = new List<string>();

		public string Status { get; set; } = StatusRequested;

		// What to change, when they asked for changes.
		public string Comment { get; set; }

		public DateTime RequestedAt { get; set; }

		public string RequestedBy { get; set; }

		public DateTime? DecidedAt { get; set; }
	}

	// One project's "work completed this period" on an invoice.
	internal class InvoiceWorkNote
	{
		[BsonRepresentation(BsonType.ObjectId)]
		public string ProjectId { get; set; }

		public string Text { get; set; }
	}

	// A time entry as the invoice billed it.
	internal class UserTimeEntry
	{
		[BsonRepresentation(BsonType.ObjectId)]
		public string TimeEntryId { get; set; }

		[BsonRepresentation(BsonType.ObjectId)]
		public string TeamMemberId { get; set; }

		// Empty on older invoices: the invoice's ProjectId.
		[BsonRepresentation(BsonType.ObjectId)]
		public string ProjectId { get; set; }

		[BsonRepresentation(BsonType.ObjectId)]
		public string ProjectTaskId { get; set; }

		[BsonRepresentation(BsonType.ObjectId)]
		public string ProjectSubTaskId { get; set; }

		public DateOnly Date { get; set; }

		public decimal Hours { get; set; }

		public string Notes { get; set; }

		public bool Billable { get; set; }

		public static UserTimeEntry From(TimeEntry entry)
		{
			return new UserTimeEntry
			{
				TimeEntryId = entry.Id,
				TeamMemberId = entry.TeamMemberId,
				ProjectId = entry.ProjectId,
				ProjectTaskId = entry.ProjectTaskId,
				ProjectSubTaskId = entry.ProjectSubTaskId,
				Date = entry.Date,
				Hours = entry.Hours,
				Notes = entry.Notes,
				Billable = entry.Billable,
			};
		}
	}
}
