using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class ConfigurationService : IConfigurationService
	{
		private ICurveDistributionRepo _curveDistributionRepo;
		private IRangeDistributionRepo _rangeDistributionRepo;

		public ConfigurationService(ICurveDistributionRepo curveDistributionRepo, IRangeDistributionRepo rangeDistributionRepo)
		{
			_curveDistributionRepo = curveDistributionRepo;
			_rangeDistributionRepo = rangeDistributionRepo;
		}

		public List<CurveDistribution> GetAllCurveDistributions()
		{
			return _curveDistributionRepo.GetAll();
		}

		public List<RangeDistribution> GetAllRangeDistributions()
		{
			return _rangeDistributionRepo.GetAll();
		}

	}
}
