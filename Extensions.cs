using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	public static class Extensions
	{

		public static string GetUserName(this ClaimsPrincipal principal)
		{
			var user = "";
			foreach (var identity in principal.Identities)
			{
				if (identity.AuthenticationType != "WebJobsAuthLevel" && identity.AuthenticationType != null)
				{
					user = identity.Name;
				}
			}
			if (string.IsNullOrWhiteSpace(user))
			{
				return "ANONYMOUS";
			}
			else
			{
				return user;
			}

		}

		public static int GetWeekOfMonth(this DateOnly date)
		{
			// Get the first day of the month
			DateTime firstDayOfMonth = new DateTime(date.Year, date.Month, 1);

			DateTime dateAsDateTime = new DateTime(date.Year, date.Month, date.Day);

			// Get the calendar-specific week of the year for both dates
			System.Globalization.Calendar calendar = CultureInfo.CurrentCulture.Calendar;
			CalendarWeekRule weekRule = CultureInfo.CurrentCulture.DateTimeFormat.CalendarWeekRule;
			DayOfWeek firstDayOfWeek = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;

			int firstWeekOfYear = calendar.GetWeekOfYear(firstDayOfMonth, weekRule, firstDayOfWeek);
			int currentWeekOfYear = calendar.GetWeekOfYear(dateAsDateTime, weekRule, firstDayOfWeek);

			// Subtracting them gives the week index within the month
			return currentWeekOfYear - firstWeekOfYear + 1;
		}
	}
}
