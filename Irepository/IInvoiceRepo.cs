using System;
using System.Collections.Generic;

namespace sma.plan
{
	internal interface IInvoiceRepo
	{
		Invoice CreateNumbered(Invoice data);
		Invoice Delete(string id);
		Invoice Get(string id);
		Invoice Find(string id);
		Invoice IssuedInvoiceBillingExpense(string expenseId);

		// Like Find, with nothing but the invoice's number and month - enough to
		// name it.
		Invoice FindNumber(string id);

		// Like Find, with nothing but what a work-completed note needs: the
		// projects it bills, whether it's cancelled, and its notes.
		Invoice FindWorkCompleted(string id);

		// Replaces the invoice's work-completed notes, and nothing else.
		void SetWorkCompleted(string id, List<InvoiceWorkNote> notes);
		Tuple<Invoice, Invoice> Update(Invoice data);

		// Invoices for lists: everything but the copies kept for their reports
		// (the time entries billed, and the project and expenses as billed). On
		// any of the projects, or every project when null; only issued ones (sent, paid
		// or cancelled) when asked - and drafts `approverId` (a team member) was
		// asked to approve. With a range, only invoices for months in it
		// (from/to, inclusive; null for open-ended) - plus, with includeOpen,
		// every invoice still open (not paid, not cancelled) whatever its month.
		List<Invoice> GetSummaries(IEnumerable<string>? projectIds, bool issuedOnly,
			DateOnly? from = null, DateOnly? to = null, bool includeOpen = false, string approverId = null);

		// Replaces the invoice's approvals, and nothing else.
		void SetApprovals(string id, List<InvoiceApproval> approvals);

		// How many drafts, not cancelled, wait for `approverId` (a team member)
		// to approve them.
		long CountAwaitingApproval(string approverId);

		// The invoices, not cancelled, already billing any of the subprojects
		// (or, made before invoices listed theirs, any of the projects) for any
		// day from `from` to `to`, other than `exceptId`. Only their number,
		// month, period and what they bill.
		List<Invoice> ActiveCovering(DateOnly from, DateOnly to, IEnumerable<string> projectIds,
			IEnumerable<string> projectTaskIds, string exceptId);
	}
}
