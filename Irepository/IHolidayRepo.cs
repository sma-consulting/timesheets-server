using System.Collections.Generic;

namespace sma.plan
{
	internal interface IHolidayRepo
	{
		Holiday Create(Holiday data);
		Holiday Delete(string id);
		bool Exists(string id);

		// Whether there are none at all - without loading any.
		bool IsEmpty();

		// All of them - about a dozen a year, so never worth filtering.
		List<Holiday> GetAll();
	}
}
