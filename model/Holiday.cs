using System;

namespace sma.plan
{
	// A day SMA takes off, from its payroll calendar - by the date actually taken
	// off, so a holiday on a weekend is listed on the weekday instead (Boxing Day
	// 2026 is a Saturday: Monday Dec 28). On the Staff hours page a holiday
	// expects no hours, so a week with one expects 32h, not 40h.
	internal class Holiday : GeneralModel
	{
		public DateOnly Date { get; set; }

		// A key into staffHours.holidays in the frontend's locale files
		// ("christmas"), so the name shows in the user's language. A name with
		// no translation there shows as it's stored.
		public string Name { get; set; }
	}
}
