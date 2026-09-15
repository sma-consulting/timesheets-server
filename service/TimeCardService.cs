using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{

	internal class TimeCardService : ITimeCardService
	{
		private ITeamMemberService _teamMemberService;
		private ITimeCardRepo _timeCardRepo;

		public TimeCardService(ITeamMemberService teamMemberService, ITimeCardRepo timeCardRepo)
		{
			_teamMemberService = teamMemberService;
			_timeCardRepo = timeCardRepo;
		}
		public TimeCard CreateTimeCard(string teamMemberId, DateOnly month)
		{
			var firstOfMonth = new DateOnly(month.Year, month.Month, 1);
			var daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
			TeamMember tm = _teamMemberService.Get(teamMemberId);
			var result = new TimeCard();
			result.TeamMemberId = tm.Id;
			var currWeek = 0;
			var prevDayOfWeek = 0;
			var weeks = result.Weeks;
			for (int i = 0; i < daysInMonth; i++)
			{
				var date = firstOfMonth.AddDays(i);
				var dayOfWeek = (int)date.DayOfWeek;
				if (dayOfWeek < prevDayOfWeek)
				{
					currWeek++;
				}
				weeks[currWeek].SetDayByIndex(dayOfWeek, date);
				prevDayOfWeek = dayOfWeek;
				
			}
			result = _timeCardRepo.Create(result);

			return result;
		}
	}
}
