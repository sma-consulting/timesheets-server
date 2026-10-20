using System;
using System.Globalization;

namespace sma.plan
{
	// The holidays the database starts with, added at startup only while the
	// collection is empty - so a holiday an admin removes stays removed, and from
	// then on the database is the list. New years are added through the holiday
	// API from SMA's payroll calendar.
	//
	// 2026 is SMA's 2026 payroll calendar. 2025 and 2027 are as the frontend
	// listed them before they moved here (src/config/holidays.js), which has
	// neither Easter Monday nor Boxing Day: check them against those years'
	// payroll calendars.
	internal static class HolidaySeed
	{
		private static readonly (string Date, string Name)[] Holidays =
		{
			// 2025
			("2025-01-01", "newYear"),
			("2025-02-17", "familyDay"),
			("2025-04-18", "goodFriday"),
			("2025-05-19", "victoriaDay"),
			("2025-07-01", "canadaDay"),
			("2025-08-04", "heritageDay"),
			("2025-09-01", "labourDay"),
			("2025-09-30", "truthReconciliation"),
			("2025-10-13", "thanksgiving"),
			("2025-11-11", "remembranceDay"),
			("2025-12-25", "christmas"),

			// 2026 - SMA payroll calendar
			("2026-01-01", "newYear"),
			("2026-02-16", "familyDay"),
			("2026-04-03", "goodFriday"),
			("2026-04-06", "easterMonday"),
			("2026-05-18", "victoriaDay"),
			("2026-07-01", "canadaDay"),
			("2026-08-03", "heritageDay"),
			("2026-09-07", "labourDay"),
			("2026-09-30", "truthReconciliation"),
			("2026-10-12", "thanksgiving"),
			("2026-11-11", "remembranceDay"),
			("2026-12-25", "christmas"),
			("2026-12-28", "boxingDay"),

			// 2027
			("2027-01-01", "newYear"),
			("2027-02-15", "familyDay"),
			("2027-03-26", "goodFriday"),
			("2027-05-24", "victoriaDay"),
			("2027-07-01", "canadaDay"),
			("2027-08-02", "heritageDay"),
			("2027-09-06", "labourDay"),
			("2027-09-30", "truthReconciliation"),
			("2027-10-11", "thanksgiving"),
			("2027-11-11", "remembranceDay"),
			("2027-12-27", "christmas"),
		};

		public static void Run(IHolidayRepo repo)
		{
			if (repo.GetAll().Count > 0)
			{
				return;
			}

			foreach (var (date, name) in Holidays)
			{
				repo.Create(new Holiday
				{
					Date = DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture),
					Name = name,
				});
			}
		}
	}
}
