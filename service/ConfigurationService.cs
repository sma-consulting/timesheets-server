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
		private ISecurityService _securityService;

		public ConfigurationService(ICurveDistributionRepo curveDistributionRepo, IRangeDistributionRepo rangeDistributionRepo, ISecurityService securityService) 
		{ 
			_curveDistributionRepo = curveDistributionRepo;
			_rangeDistributionRepo = rangeDistributionRepo;	
			_securityService = securityService;
		}

		public List<CurveDistribution> GetAllCurveDistributions()
		{
			_securityService.AuthorizeUser();
			return _curveDistributionRepo.GetAll();
		}

		public List<RangeDistribution> GetAllRangeDistributions()
		{
			_securityService.AuthorizeUser();
			return _rangeDistributionRepo.GetAll();
		}

	}
}
