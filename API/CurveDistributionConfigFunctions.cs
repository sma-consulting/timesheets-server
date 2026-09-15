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
    internal class CurveDistributionConfigFunctions : RequestHandler
    {
        private IConfigurationService _configurationService;
        private ILogger<CurveDistributionConfigFunctions> _logger;

        public CurveDistributionConfigFunctions(IConfigurationService configurationService, ILogger<CurveDistributionConfigFunctions> logger) { 
            _configurationService = configurationService;
            _logger = logger;
        }

        [FunctionName("GetCurveDistribution")]
        public async Task<IActionResult> RunGetCurveDistribution(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "curveDistribution/{id}/")] HttpRequest req, string id)
        {
            return Ok(
                () => new DatabaseRepo<CurveDistribution>().Get(id),
                (p) => new
                {
                    curveDistribution = p
                });
        }

		[FunctionName("GetAllCurveDistributions")]
		public async Task<IActionResult> RunGetAllCurveDistributions(
	[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "curveDistribution/")] HttpRequest req )
		{
			return Ok(
				() => _configurationService.GetAllCurveDistributions(),
				(p) => new
				{
					curveDistribution = p
				});
		}


		[FunctionName("UpdateCurveDistribution")]
        public async Task<IActionResult> RunUpdateCurveDistribution(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "curveDistribution/{id}/update/")] HttpRequest req , string id)
        {
            CurveDistribution curveDistribution = JsonConvert.DeserializeObject<CurveDistribution>(
                await new StreamReader(req.Body).ReadToEndAsync());
            curveDistribution.Id = id;

            return Ok(
                () => new DatabaseRepo<CurveDistribution>().Update(curveDistribution),
                (p) => new
                {
                    oldCurveDistribution = ((Tuple<CurveDistribution, CurveDistribution>)p).Item1,
                    newCurveDistribution = ((Tuple<CurveDistribution, CurveDistribution>)p).Item2,
                });
        }
    }
}
