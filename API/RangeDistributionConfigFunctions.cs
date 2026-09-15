using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using MongoDB.Driver.Core.Events;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;


namespace sma.plan
{
    internal class RangeDistributionConfigFunctions : RequestHandler
    {
        private IConfigurationService _configurationService;
        private ILogger<RangeDistributionConfigFunctions> _logger;

		public RangeDistributionConfigFunctions(IConfigurationService configurationService, ILogger<RangeDistributionConfigFunctions> logger) 
        {
            _configurationService = configurationService;
            _logger = logger;
        }

        [FunctionName("GetRangeDistribution")]
        public async Task<IActionResult> RunGetRangeDistribution(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "rangeDistribution/{id}/")] HttpRequest req, string id)
        {
            return Ok(
                () => (new DatabaseRepo<RangeDistribution>()).Get(id),
                (p) => new
                {
                    rangeDistribution = p
                });
        }

		[FunctionName("GetAllRangeDistributions")]
		public async Task<IActionResult> RunGetAllRangeDistributions(
	[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "rangeDistribution/")] HttpRequest req)
		{
			return Ok(
				() => _configurationService.GetAllRangeDistributions(),
				(p) => new
				{
					rangeDistribution = p
				});
		}


		[FunctionName("UpdateRangeDistribution")]
        public async Task<IActionResult> RunUpdateRangeDistribution(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "rangeDistribution/{id}/update/")] HttpRequest req, string id)
        {
            RangeDistribution rangeDistribution = JsonConvert.DeserializeObject<RangeDistribution>(
                await new StreamReader(req.Body).ReadToEndAsync());
            rangeDistribution.Id = id;

            return Ok(
                () => (new DatabaseRepo<RangeDistribution>()).Update(rangeDistribution),
                (p) => new
                {
                    oldRangeDistribution = ((Tuple<RangeDistribution, RangeDistribution>)p).Item1,
                    newRangeDistribution = ((Tuple<RangeDistribution, RangeDistribution>)p).Item2,
                });
        }
    }
}
