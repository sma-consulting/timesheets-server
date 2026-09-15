using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	/**
	 * Modestly clunky API to accommodate data model build for human readable friendliness.
	 */
	internal class TimeCardWeek
	{

		public List<string> SundayEntries { get; set; } = new List<string>();
		public DateOnly Sunday { get; set; }

		public List<string> MondayEntries { get; set; } = new List<string>();
		public DateOnly Monday { get; set; }

		public List<string> TuesdayEntries { get; set; } = new List<string>();
		public DateOnly Tuesday { get; set; }

		public List<string> WednesdayEntries { get; set; } = new List<string>();
		public DateOnly Wednesday { get; set; }

		public List<string> ThursdayEntries { get; set; } = new List<string>();
		public DateOnly Thursday { get; set; }

		public List<string> FridayEntries { get; set; } = new List<string>();
		public DateOnly Friday { get; set; }

		public List<string> SaturdayEntries { get; set; } = new List<string>();
		public DateOnly Saturday { get; set; }

		public DateOnly GetDayByIndex(int i)
		{
			switch (i)
			{
				case 0: return Sunday;
				case 1: return Monday;
				case 2: return Tuesday;
				case 3: return Wednesday;
				case 4: return Thursday;
				case 5: return Friday;
				case 6: return Saturday;
				default: throw new ArgumentException();
			}
		}

		public void SetDayByIndex(int i, DateOnly date)
		{
			switch (i)
			{
				case 0: Sunday = date; break;
				case 1: Monday = date; break;
				case 2: Tuesday = date; break;
				case 3: Wednesday = date; break;
				case 4: Thursday = date; break;
				case 5: Friday = date; break;
				case 6: Saturday = date; break;
				default: throw new ArgumentException();
			}
		}

		public List<string> GetEntriesByIndex(int i)
		{
			switch (i)
			{
				case 0: return SundayEntries;
				case 1: return MondayEntries;
				case 2: return TuesdayEntries;
				case 3: return WednesdayEntries;
				case 4: return ThursdayEntries;
				case 5: return FridayEntries;
				case 6: return SaturdayEntries;
				default: throw new ArgumentException();
			}
		}

		public (DateOnly date,  List<string> entries) GetByIndex(int i)
		{
			switch (i)
			{
				case 0: return (Sunday, SundayEntries);
				case 1: return (Monday, MondayEntries);
				case 2: return (Tuesday, TuesdayEntries);
				case 3: return (Wednesday, WednesdayEntries);
				case 4: return (Thursday, ThursdayEntries);
				case 5: return (Friday, FridayEntries);
				case 6: return (Saturday, SaturdayEntries);
				default: throw new ArgumentException();
			}
		}

	}
}
