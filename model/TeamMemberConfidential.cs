using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class TeamMemberConfidential : GeneralModel
	{

		public string TeamMemberId { get; set; }

		public List<TeamMemberCostRate> CostRates { get; set; }
	}

	public class TeamMemberCostRate
	{
		public DateOnly StartDate { get; set; }

		public DateOnly EndDate { get; set; }

		public decimal Rate { get; set; }
	}
}
