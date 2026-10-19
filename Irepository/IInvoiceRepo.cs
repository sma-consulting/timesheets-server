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

		// Issued invoices (sent, paid or cancelled) on any of the projects.
		List<Invoice> GetIssuedOnProjects(IEnumerable<string> projectIds);

		// The project's invoices that aren't cancelled.
		List<Invoice> GetActiveOnProject(string projectId);
	}
}
