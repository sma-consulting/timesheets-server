using System.Collections.Generic;
using System.Linq;

namespace sma.plan
{
	// Time entries have said whether they've been invoiced (TimeEntry.Invoiced)
	// since October 2026; issuing and cancelling keep it up to date. Entries
	// billed on invoices issued before then are marked here, once, in the
	// background at startup. After that the counts agree and it stops there.
	internal static class InvoicedTimeSync
	{
		public static void Run(IInvoiceRepo invoiceRepo, ITimeEntryRepo timeEntryRepo)
		{
			List<Invoice> issued = invoiceRepo.GetAll()
				.Where(i => i.Issued && !i.Cancelled)
				.ToList();

			Dictionary<string, List<string>> billed = issued.ToDictionary(
				i => i.Id,
				i => (i.TimeEntries ?? new List<UserTimeEntry>())
					.Select(e => e.TimeEntryId)
					.Where(id => !string.IsNullOrWhiteSpace(id))
					.Distinct()
					.ToList());

			long expected = billed.Values.Sum(ids => (long)ids.Count);
			if (expected == 0 || timeEntryRepo.CountInvoiced() >= expected)
			{
				return;
			}

			foreach (KeyValuePair<string, List<string>> invoice in billed)
			{
				timeEntryRepo.MarkInvoiced(invoice.Value, invoice.Key);
			}
		}
	}
}
