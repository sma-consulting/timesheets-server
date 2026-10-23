using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal interface ITimeEntryService
	{

		TimeEntry Create(TimeEntry timeEntry);

		TimeEntry Update(TimeEntry timeEntry);

		TimeEntry Get(string id);

		TimeEntry Delete(string id);

		List<TimeEntry> GetAllTimeEntries();

		// Out of those GetAllTimeEntries would give, the ones for a person,
		// project and/or days - a null one doesn't narrow - without their history.
		List<TimeEntry> GetTimeEntries(string teamMemberId, string projectId, DateOnly? from, DateOnly? to);

		// The caller's own entries - admins and project managers included.
		List<TimeEntry> GetOwnTimeEntries();
		bool MayLogAgainst(string teamMemberId, string projectSubTaskId);
		bool MayWrite(string timeEntryId);
		bool MayRead(string timeEntryId);
		string Validate(TimeEntry timeEntry);
		string OwnerFor(TimeEntry timeEntry);
		string LockedReason(string timeEntryId, TimeEntry changed = null);
	}
}
