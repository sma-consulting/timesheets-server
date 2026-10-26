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
		Invoice IssuedInvoiceBilling(string timeEntryId);
		Invoice IssuedInvoiceBillingExpense(string expenseId);
		List<Invoice> GetAll();
		Tuple<Invoice, Invoice> Update(Invoice data);

		// Invoices for lists: everything but the copies kept for their reports
		// (the time entries billed, and the project and expenses as billed). On
		// the projects, or every project when null; only issued ones (sent, paid
		// or cancelled) when asked. With a range, only invoices for months in it
		// (from/to, inclusive; null for open-ended) - plus, with includeOpen,
		// every invoice still open (not paid, not cancelled) whatever its month.
		List<Invoice> GetSummaries(IEnumerable<string>? projectIds, bool issuedOnly,
			DateOnly? from = null, DateOnly? to = null, bool includeOpen = false);

		// The project's invoices that aren't cancelled.
		List<Invoice> GetActiveOnProject(string projectId);

		// The issued invoices that aren't cancelled, with nothing but their id
		// and the ids of the time entries each billed (TimeEntries[].TimeEntryId).
		List<Invoice> GetIssuedTimeEntryIds();
	}
}
