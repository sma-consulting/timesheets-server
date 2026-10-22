using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;

namespace sma.plan
{
	internal class ExpenseRepo : DatabaseRepo<Expense>, IExpenseRepo
	{
		public List<Expense> GetByTeamMember(string teamMemberId)
		{
			var filter = Builders<Expense>.Filter.Eq(e => e.TeamMemberId, teamMemberId);
			return DBCollection.Find(filter).ToList();
		}

		public List<Expense> GetByIds(IEnumerable<string> ids)
		{
			var filter = Builders<Expense>.Filter.In(e => e.Id, ValidIds(ids));
			return DBCollection.Find(filter).SortBy(e => e.Id).ToList();
		}

		public List<Expense> GetByProjects(IEnumerable<string> projectIds)
		{
			var filter = Builders<Expense>.Filter.In(e => e.ProjectId, ValidIds(projectIds));
			return DBCollection.Find(filter).SortBy(e => e.Id).ToList();
		}

		public List<Expense> GetFiltered(string? teamMemberId, string? projectId, DateOnly? from, DateOnly? to)
		{
			var f = Builders<Expense>.Filter;
			var filter = DateBetween(nameof(Expense.Date), from, to);

			foreach ((string id, bool isMember) in new[] { (teamMemberId, true), (projectId, false) })
			{
				if (id == null)
				{
					continue;
				}
				// An id that isn't an ObjectId can't match, and would throw in a filter.
				if (!ObjectId.TryParse(id, out _))
				{
					return new List<Expense>();
				}
				filter &= isMember ? f.Eq(e => e.TeamMemberId, id) : f.Eq(e => e.ProjectId, id);
			}

			// The exact range: the query narrows by month (see DateBetween).
			return DBCollection.Find(filter).SortBy(e => e.Id).ToList()
				.Where(e => (from == null || e.Date >= from.Value) && (to == null || e.Date <= to.Value))
				.ToList();
		}

		// The flags are matched with Ne ("not equal") rather than Eq: an expense
		// saved before a flag existed has no such field, and C# reads it back as
		// the property's default. So "not true" is what false means here.
		public List<Expense> GetUninvoicedBillable(IEnumerable<string>? projectIds)
		{
			var filter = Builders<Expense>.Filter.Eq(e => e.Status, Expense.StatusApproved)
				& Builders<Expense>.Filter.Eq(e => e.Billable, true)
				& Builders<Expense>.Filter.Ne(e => e.Invoiced, true);

			if (projectIds != null)
			{
				filter &= Builders<Expense>.Filter.In(e => e.ProjectId, ValidIds(projectIds));
			}

			return DBCollection.Find(filter).SortBy(e => e.Id).ToList();
		}

		// Reimbursable defaults to true in C#, so an expense without the field is
		// reimbursable: "not false".
		public List<Expense> GetAwaitingReimbursement()
		{
			var filter = Builders<Expense>.Filter.Eq(e => e.Status, Expense.StatusApproved)
				& Builders<Expense>.Filter.Ne(e => e.Reimbursable, false)
				& Builders<Expense>.Filter.Ne(e => e.Reimbursed, true);
			return DBCollection.Find(filter).SortBy(e => e.Id).ToList();
		}

		public List<Expense> GetByReimbursementBatch(string batchId)
		{
			if (!ObjectId.TryParse(batchId, out _))
			{
				return new List<Expense>();
			}

			var filter = Builders<Expense>.Filter.Eq(e => e.ReimbursementBatchId, batchId);
			return DBCollection.Find(filter).SortBy(e => e.Id).ToList();
		}
	}
}
