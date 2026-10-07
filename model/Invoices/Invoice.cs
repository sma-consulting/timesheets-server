using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace sma.plan
{
	// A project's invoice for one calendar month. Built on the Invoices page from
	// the month's hours and expenses, then edited there; what's saved is the
	// invoice as edited, so its figures never move once it's sent, whatever
	// happens to the time entries or rates later.
	internal class Invoice : GeneralModel
	{
		[BsonRepresentation(BsonType.ObjectId)]
		public string ProjectId { get; set; }

		// The month billed, kept as its last day: 2026-09-30 is September 2026.
		// One invoice per project per month, not counting cancelled ones.
		public DateOnly Month { get; set; }

		// Given by the server when the invoice is first saved: one past the
		// highest so far, carrying on from SMA's own numbering (Sage's InvoiceID).
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

		public List<InvoiceApproval> Approvals { get; set; } = new List<InvoiceApproval>();

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

		public List<InvoiceTax> Taxes { get; set; } = new List<InvoiceTax>();
	}

	// A row of the invoice table: a person's hours at their rate, the flat
	// disbursement, an expense at cost, a lump-sum fee or a line added by hand.
	// Which fields a row has depends on its kind, so empty ones are left out
	// rather than stored or sent as null - the page tells the kinds apart by
	// what's there.
	internal class InvoiceLine
	{
		// The page's own key for the row.
		public string Id { get; set; }

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

		// Set instead of quantity × rate on a lump-sum fee or an expense.
		[BsonIgnoreIfNull, JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public decimal? Amount { get; set; }

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

	// Someone signing the invoice off.
	internal class InvoiceApproval
	{
		[BsonRepresentation(BsonType.ObjectId)]
		public string UserId { get; set; }

		public DateTime ApprovedAt { get; set; }
	}

	// A time entry as the invoice billed it.
	internal class UserTimeEntry
	{
		[BsonRepresentation(BsonType.ObjectId)]
		public string TimeEntryId { get; set; }

		[BsonRepresentation(BsonType.ObjectId)]
		public string TeamMemberId { get; set; }

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
