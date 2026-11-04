using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using System.IO;
using System.Threading.Tasks;

namespace sma.plan
{
	// Imports the last two months from Harvest - see HarvestImport. Only where
	// it's enabled: ENABLE_DEV_SEED on and a database named ...HARVEST, so it
	// can never touch real data; and only with HARVEST_TOKEN_FILE set, which is
	// a local setting. With ACCESS_EXPORT_FOLDER set too, Access's tables
	// overrule Harvest's. Open to anyone signed in there: a fresh database has
	// no admins yet, and running it makes the caller one.
	internal class HarvestImportFunctions : RequestHandler
	{
		private readonly ISecurityService _securityService;
		private readonly IProjectRepo _projectRepo;
		private readonly IProjectTaskRepo _taskRepo;
		private readonly IProjectSubTaskRepo _subTaskRepo;

		public HarvestImportFunctions(
			ISecurityService securityService,
			IProjectRepo projectRepo,
			IProjectTaskRepo taskRepo,
			IProjectSubTaskRepo subTaskRepo)
		{
			_securityService = securityService;
			_projectRepo = projectRepo;
			_taskRepo = taskRepo;
			_subTaskRepo = subTaskRepo;
		}

		// Empties the HARVEST database and fills it from Harvest.
		[Allow(Role.SignedIn)]
		[Function("ImportHarvest")]
		public async Task<IActionResult> RunImportHarvest(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "dev/harvest-import/")] HttpRequest req)
		{
			if (!HarvestImport.Enabled())
			{
				return Forbidden(Errors.Dev.HarvestOff);
			}
			string tokenFile = HarvestImport.TokenFile();
			if (string.IsNullOrWhiteSpace(tokenFile) || !File.Exists(tokenFile))
			{
				return Invalid(Errors.Dev.HarvestToken);
			}
			// Set, the folder must hold Access's exports - an import without them
			// would quietly be Harvest's alone.
			string accessFolder = HarvestImport.AccessExportFolder();
			AccessExport access = null;
			if (!string.IsNullOrWhiteSpace(accessFolder))
			{
				access = Directory.Exists(accessFolder) ? AccessExport.Load(accessFolder) : null;
				if (access == null || access.Files.Count == 0)
				{
					return Invalid(Errors.Dev.AccessExport);
				}
			}

			// Read before the wipe, which takes the caller's records with it.
			string email = _securityService.GetCurrentEmail();

			return Ok(
				() =>
				{
					DevSeed.Wipe(HarvestImport.DatabaseSuffix);

					// The paid hours off project, as at startup, for holidays,
					// leave and lunch breaks.
					PaidTimeOffSeed.Run(_projectRepo, _taskRepo, _subTaskRepo);

					return new HarvestImporter(tokenFile, email,
						PaidTimeOffSeed.ByType(_projectRepo, _taskRepo, _subTaskRepo),
						access).Run();
				},
				(summary) => new
				{
					summary
				});
		}
	}
}
