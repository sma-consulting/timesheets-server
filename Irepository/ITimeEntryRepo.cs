using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal interface ITimeEntryRepo
	{
		TimeEntry Create(TimeEntry data);
		TimeEntry Delete(string id);
		TimeEntry Get(string id);
		List<TimeEntry> GetAll();
		Tuple<TimeEntry, TimeEntry> Update(TimeEntry data);

		// Marks the entries as billed on the invoice. Ones already marked with it
		// aren't written again.
		void MarkInvoiced(IEnumerable<string> timeEntryIds, string invoiceId);

		// Unmarks whatever was billed on the invoice - it's been cancelled.
		void ClearInvoiced(string invoiceId);

		// How many entries are marked invoiced.
		long CountInvoiced();

		
		// The team member's own entries, plus everyone's on the given projects.
		List<TimeEntry> GetVisibleTo(string teamMemberId, IEnumerable<string> projectIds);
				
		// How many entries are logged against any of the tasks or sub-tasks.
		long CountLoggedAgainst(IEnumerable<string> projectTaskIds, IEnumerable<string> projectSubTaskIds);

		// Every entry on the project.
		List<TimeEntry> GetByProject(string projectId);


	}
}
