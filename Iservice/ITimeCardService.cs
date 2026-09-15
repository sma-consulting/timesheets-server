using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal interface ITimeCardService
	{

		TimeCard CreateTimeCard(string teamMemberId, DateOnly month);
	}
}
