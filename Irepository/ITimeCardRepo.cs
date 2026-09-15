using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal interface ITimeCardRepo
	{
		TimeCard Create(TimeCard data);
		TimeCard Delete(string id);
		TimeCard Get(string id);
		List<TimeCard> GetAll();
		Task<List<TimeCard>> GetByTeamMember(string teamMemberId, DateOnly cutoffDate);
		Tuple<TimeCard, TimeCard> Update(TimeCard data);
	}
}
