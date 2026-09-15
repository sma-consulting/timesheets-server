using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal interface ICurveDistributionRepo
	{
		CurveDistribution Create(CurveDistribution data);
		CurveDistribution Delete(string id);
		CurveDistribution Get(string id);
		List<CurveDistribution> GetAll();
		Tuple<CurveDistribution, CurveDistribution> Update(CurveDistribution data);
	}
}
