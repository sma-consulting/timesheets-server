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
	}
}
