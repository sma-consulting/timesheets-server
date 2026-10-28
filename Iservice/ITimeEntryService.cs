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

		// The entry as stored, or null. A request reads it once with this and
		// hands it to the checks and the save below, which read nothing more.
		TimeEntry Find(string id);

		TimeEntry Update(TimeEntry timeEntry, TimeEntry existing);

		TimeEntry Delete(TimeEntry existing);

		// The entries the caller may see for a person, project and/or days - a
		// null one doesn't narrow - without their history.
		List<TimeEntry> GetTimeEntries(string teamMemberId, string projectId, DateOnly? from, DateOnly? to);

		// After Validate, which fills in the entry's project.
		bool MayLogAgainst(string teamMemberId, string projectId);
		bool MayWrite(TimeEntry existing);
		bool MayRead(TimeEntry existing);
		string Validate(TimeEntry timeEntry);
		string OwnerFor(TimeEntry timeEntry);
		string LockedReason(TimeEntry existing, TimeEntry changed = null);

		// Locks or unlocks the time between two days, for one person or everyone
		// (null), on one project or all (null). How many entries changed.
		long Lock(string teamMemberId, string projectId, DateOnly from, DateOnly to, bool locked);
	}
}
