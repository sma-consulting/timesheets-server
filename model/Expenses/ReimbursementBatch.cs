using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace sma.plan
{
	// A reimbursement batch, filed on the 15th: every approved, reimbursable
	// expense not yet paid back and dated on or before BatchDate, grouped by
	// person. Filing it marks its expenses reimbursed, so no later batch pays
	// them again; voiding one filed by mistake frees them for the next. The
	// payment itself is made outside the app.
	internal class ReimbursementBatch : GeneralModel
	{
		public DateOnly BatchDate { get; set; }

		public DateTime FiledAt { get; set; }

		// The User who filed it.
		[BsonRepresentation(BsonType.ObjectId)]
		public string FiledBy { get; set; }

		// Who it pays and what, as it was when filed - so its PDF can be printed
		// again exactly.
		public List<ReimbursementPerson> People { get; set; } = new List<ReimbursementPerson>();

		public decimal Total { get; set; }

		public int Count { get; set; }

		public bool Voided { get; set; }

		public DateTime? VoidedAt { get; set; }

		[BsonRepresentation(BsonType.ObjectId)]
		public string VoidedBy { get; set; }
	}

	// One person's share of a batch.
	internal class ReimbursementPerson
	{
		[BsonRepresentation(BsonType.ObjectId)]
		public string TeamMemberId { get; set; }

		// Their name when the batch was filed.
		public string Name { get; set; }

		// Their expenses in the batch, in date order, copied as they were.
		public List<Expense> Expenses { get; set; } = new List<Expense>();

		public decimal Subtotal { get; set; }
	}
}
