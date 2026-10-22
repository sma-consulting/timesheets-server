using System;
using System.Collections.Generic;

namespace sma.plan
{
	internal interface IExpenseRepo
	{
		Expense Create(Expense data);
		Expense Delete(string id);
		Expense Get(string id);
		List<Expense> GetAll();
		Tuple<Expense, Expense> Update(Expense data);
		List<Expense> GetByTeamMember(string teamMemberId);

		// The expenses with these ids.
		List<Expense> GetByIds(IEnumerable<string> ids);

		// The expenses on any of the projects.
		List<Expense> GetByProjects(IEnumerable<string> projectIds);

		// The expenses matching every filter given - one team member's, on one
		// project, dated from/to (inclusive) - each null to leave it out.
		List<Expense> GetFiltered(string? teamMemberId, string? projectId, DateOnly? from, DateOnly? to);

		// Approved, billable, not yet invoiced - on the projects, or on every
		// project when projectIds is null.
		List<Expense> GetUninvoicedBillable(IEnumerable<string>? projectIds);

		// Approved, reimbursable, not yet reimbursed - whatever the date.
		List<Expense> GetAwaitingReimbursement();

		// The expenses reimbursed in the batch.
		List<Expense> GetByReimbursementBatch(string batchId);
	}
}
