using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Linq;

namespace sma.plan
{
	internal class InvoiceRepo : DatabaseRepo<Invoice>, IInvoiceRepo
	{
		// The last invoice number SMA used before this app, so the app's numbers
		// carry on from it rather than clash in Sage.
		private const string LastInvoiceNumberSetting = "LAST_INVOICE_NUMBER";

		private const int NumberAttempts = 5;

		public InvoiceRepo()
		{
			// No two invoices may share a number. Cosmos only builds a unique
			// index on an empty collection, so it's made here, before the first
			// invoice is saved.
			try
			{
				DBCollection.Indexes.CreateOne(new CreateIndexModel<Invoice>(
					Builders<Invoice>.IndexKeys.Ascending(i => i.Number),
					new CreateIndexOptions { Unique = true }));
			}
			catch (MongoException ex)
			{
				Console.WriteLine("Could not create the unique index on invoice numbers: " + ex.Message);
			}
		}

		// Saves a new invoice under the next number: one past the highest so far,
		// or past LAST_INVOICE_NUMBER while that's higher. Two invoices saved at
		// the same moment would pick the same number; the unique index turns the
		// second away, and it moves on to the one after.
		public Invoice CreateNumbered(Invoice invoice)
		{
			int.TryParse(Environment.GetEnvironmentVariable(LastInvoiceNumberSetting), out int lastBeforeApp);
			int next = Math.Max(HighestNumber(), lastBeforeApp) + 1;

			for (int attempt = 1; ; attempt++)
			{
				invoice.Number = next;
				invoice.Id = null;

				try
				{
					DBCollection.InsertOne(invoice);
					return Get(invoice.Id);
				}
				catch (MongoWriteException ex)
					when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey && attempt < NumberAttempts)
				{
					// Taken a moment ago by another save: the one after it, never
					// the same one again.
					Console.WriteLine("Invoice number " + next + " is taken; trying the next.");
					next = Math.Max(next, HighestNumber()) + 1;
				}
			}
		}

		// The highest invoice number so far; 0 with none. Read as plain documents
		// ({ Number: 1 }) rather than through a LINQ projection, which the driver
		// can send as an expression Cosmos doesn't evaluate as MongoDB does - on
		// the sandbox new invoices kept clashing with a number already taken.
		private int HighestNumber()
		{
			return DBCollection
				.Find(FilterDefinition<Invoice>.Empty)
				.Project<BsonDocument>(Builders<Invoice>.Projection.Include(i => i.Number).Exclude(i => i.Id))
				.ToList()
				.Select(d => d.TryGetValue(nameof(Invoice.Number), out BsonValue n) && n.IsNumeric ? n.ToInt32() : 0)
				.DefaultIfEmpty(0)
				.Max();
		}

		// Like Get, but null rather than an exception when there's no such invoice
		// (or the id isn't one at all).
		public Invoice Find(string id)
		{
			if (!ObjectId.TryParse(id, out _))
			{
				return null;
			}
			return DBCollection.Find(i => i.Id == id).FirstOrDefault();
		}

		// The issued, not cancelled invoice that billed a time entry, if any.
		public Invoice IssuedInvoiceBilling(string timeEntryId)
		{
			if (!ObjectId.TryParse(timeEntryId, out _))
			{
				return null;
			}

			var filter = Builders<Invoice>.Filter.Eq(i => i.Issued, true)
				& Builders<Invoice>.Filter.Eq(i => i.Cancelled, false)
				& Builders<Invoice>.Filter.ElemMatch(i => i.TimeEntries, e => e.TimeEntryId == timeEntryId);
			return DBCollection.Find(filter).FirstOrDefault();
		}

		// The issued, not cancelled invoice that bills an expense, if any.
		public Invoice IssuedInvoiceBillingExpense(string expenseId)
		{
			if (string.IsNullOrWhiteSpace(expenseId))
			{
				return null;
			}

			var filter = Builders<Invoice>.Filter.Eq(i => i.Issued, true)
				& Builders<Invoice>.Filter.Eq(i => i.Cancelled, false)
				& Builders<Invoice>.Filter.ElemMatch(i => i.Lines, l => l.ExpenseId == expenseId);
			return DBCollection.Find(filter).FirstOrDefault();
		}
	}
}
