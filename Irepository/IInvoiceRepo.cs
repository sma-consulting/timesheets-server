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
		List<Invoice> GetAll();
		Tuple<Invoice, Invoice> Update(Invoice data);

		// Invoices for lists: everything but the copies kept for their reports
		// (the time entries billed, and the project and expenses as billed). On
		// any of the projects, or every project when null; only issued ones (sent, paid
		// or cancelled) when asked. With a range, only invoices for months in it
		// (from/to, inclusive; null for open-ended) - plus, with includeOpen,
		// every invoice still open (not paid, not cancelled) whatever its month.
		List<Invoice> GetSummaries(IEnumerable<string>? projectIds, bool issuedOnly,
			DateOnly? from = null, DateOnly? to = null, bool includeOpen = false);

		// The month's invoices, not cancelled, already billing any of the
		// subprojects (or, made before invoices listed theirs, any of the
		// projects), other than `exceptId`. Only their number, month and what
		// they bill.
		List<Invoice> ActiveCovering(DateOnly month, IEnumerable<string> projectIds,
			IEnumerable<string> projectTaskIds, string exceptId);

		// The issued invoices that aren't cancelled, with nothing but their id
		// and the ids of the time entries each billed (TimeEntries[].TimeEntryId).
		List<Invoice> GetIssuedTimeEntryIds();
	}
}
