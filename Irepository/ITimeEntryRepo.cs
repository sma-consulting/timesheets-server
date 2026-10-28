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

		// The entry, or null when there's none with the id (or it isn't an id).
		TimeEntry Find(string id);

		// Saving without reading back: the entry as written is what's returned.
		// Insert gives a new entry its id.
		TimeEntry Insert(TimeEntry data);
		TimeEntry Replace(TimeEntry data);

		// Deletes the entry, reading nothing first.
		void Remove(string id);

		// Marks the entries as billed on the invoice. Ones already marked with it
		// aren't written again.
		void MarkInvoiced(IEnumerable<string> timeEntryIds, string invoiceId);

		// Unmarks whatever was billed on the invoice - it's been cancelled.
		void ClearInvoiced(string invoiceId);

		// Locks the time between two days (when and by whom), or unlocks it with
		// nulls, for one person or everyone (null), on one project or all (null).
		// How many entries were changed.
		long SetLocked(string teamMemberId, string projectId, DateOnly from, DateOnly to,
			DateTime? lockedAt, string lockedBy);

		// How many entries are marked invoiced.
		long CountInvoiced();


		// The entries matching every filter given - a null one doesn't narrow -
		// without their edit history. With `visibleTo` set, only that team
		// member's own entries and those on `visibleProjectIds` (someone who
		// isn't an admin); null leaves them all in.
		List<TimeEntry> GetFiltered(string teamMemberId, string projectId, DateOnly? from, DateOnly? to,
			string visibleTo, IEnumerable<string> visibleProjectIds);
				
		// How many entries are logged against any of the tasks or sub-tasks.
		long CountLoggedAgainst(IEnumerable<string> projectTaskIds, IEnumerable<string> projectSubTaskIds);

		// The entries on any of the subprojects between two days (inclusive),
		// without their edit history - what an invoice bills.
		List<TimeEntry> GetOnTasks(IEnumerable<string> projectTaskIds, DateOnly from, DateOnly to);

		// Everyone's entries between two days (inclusive), with only what adding
		// up hours needs: who, the project and sub-task, the day, the hours, and
		// whether they're billed or locked. The notes and the edit history aren't
		// loaded.
		List<TimeEntry> GetHoursBetween(DateOnly from, DateOnly to);


	}
}
