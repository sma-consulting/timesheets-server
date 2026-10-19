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

		// Approved, billable, not yet invoiced - on the projects, or on every
		// project when projectIds is null.
		List<Expense> GetUninvoicedBillable(IEnumerable<string>? projectIds);

		// Approved, reimbursable, not yet reimbursed - whatever the date.
		List<Expense> GetAwaitingReimbursement();

		// The expenses reimbursed in the batch.
		List<Expense> GetByReimbursementBatch(string batchId);
	}
}
