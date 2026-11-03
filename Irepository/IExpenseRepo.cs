using System;
using System.Collections.Generic;

namespace sma.plan
{
	internal interface IExpenseRepo
	{
		Expense Create(Expense data);
		Expense Delete(string id);
		Expense Get(string id);
		Expense Find(string id);
		Tuple<Expense, Expense> Update(Expense data);

		// The expenses with these ids.
		List<Expense> GetByIds(IEnumerable<string> ids);

		// The expenses matching every filter given - one team member's, on one
		// project, dated from/to (inclusive), with one status - each null to
		// leave it out.
		List<Expense> GetFiltered(string? teamMemberId, string? projectId, DateOnly? from, DateOnly? to, string? status = null);

		// For the company-wide expense report: the expenses dated from/to
		// (inclusive) that weren't rejected, on `projectIds` or on any project
		// (null), with only what adding them up needs - no notes or receipt.
		List<Expense> GetForReport(IEnumerable<string>? projectIds, DateOnly from, DateOnly to);

		// How many have this status - those waiting for review, for the menu.
		long CountByStatus(string status);

		// Approved, billable, not yet invoiced - on the projects, or on every
		// project when projectIds is null.
		List<Expense> GetUninvoicedBillable(IEnumerable<string>? projectIds);

		// Approved, reimbursable, not yet reimbursed - whatever the date.
		List<Expense> GetAwaitingReimbursement();

		// The expenses reimbursed in the batch.
		List<Expense> GetByReimbursementBatch(string batchId);
	}
}
