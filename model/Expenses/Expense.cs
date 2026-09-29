using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace sma.plan
{
	// A reimbursable expense someone paid for on a project. The receipt file is
	// kept in its own ExpenseReceipt document, so listing expenses never loads
	// the photos.
	internal class Expense : GeneralModel
	{
		public const string StatusSubmitted = "Submitted";
		public const string StatusApproved = "Approved";
		public const string StatusRejected = "Rejected";

		// Must match EXPENSE_CATEGORIES in the frontend's model/Expense.js.
		public static readonly string[] Categories =
		{
			"Accommodation",
			"Airfare",
			"Equipment",
			"Meals",
			"Mileage",
			"Parking",
			"Printing",
			"Supplies",
			"Taxi / Transit",
			"Other",
		};

		// Categories where a note is required: meals need who and why.
		public static readonly HashSet<string> NotesRequired = new HashSet<string> { "Meals" };

		// Who claimed it - always the person who submitted it.
		[BsonRepresentation(BsonType.ObjectId)]
		public string TeamMemberId { get; set; }

		[BsonRepresentation(BsonType.ObjectId)]
		public string ProjectId { get; set; }

		// The subproject, when one was picked.
		[BsonRepresentation(BsonType.ObjectId)]
		public string ProjectTaskId { get; set; }

		public DateOnly Date { get; set; }

		public string Category { get; set; }

		public string Notes { get; set; }

		public decimal Amount { get; set; }

		public string Currency { get; set; } = "CAD";

		public bool Billable { get; set; }

		public string Status { get; set; } = StatusSubmitted;

		public DateTime SubmittedAt { get; set; }

		// The admin's decision. Cleared when the claim goes back to Submitted -
		// sent back by an admin, or changed by its owner.
		[BsonRepresentation(BsonType.ObjectId)]
		public string ReviewedBy { get; set; }

		public DateTime? ReviewedAt { get; set; }

		// Why it was rejected; required for a rejection, so the owner knows
		// what to fix.
		public string RejectionReason { get; set; }

		[BsonRepresentation(BsonType.ObjectId)]
		public string ReceiptId { get; set; }

		public string ReceiptName { get; set; }

		public string ReceiptContentType { get; set; }

		// Set by the invoice and reimbursement steps, which aren't built yet.
		public bool Invoiced { get; set; }

		public bool Reimbursed { get; set; }
	}
}
