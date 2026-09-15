using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal interface IRangeDistributionRepo
	{

		RangeDistribution Create(RangeDistribution data);
		RangeDistribution Delete(string id);
		RangeDistribution Get(string id);
		List<RangeDistribution> GetAll();
		Tuple<RangeDistribution, RangeDistribution> Update(RangeDistribution data);
	}
}
