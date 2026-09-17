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
	}
}
