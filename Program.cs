using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Microsoft.Extensions.Azure;
using sma.plan;
using System.Text.Json;
using System.Text.Json.Serialization;

// In the Isolated Worker model, the Program.cs file is the entry point
// where the application host is built and configured.
var host = new HostBuilder()
	.ConfigureFunctionsWebApplication()
	.ConfigureServices(services =>
	{
		//services.AddApplicationInsightsTelemetryWorkerService();
		//services.ConfigureFunctionsApplicationInsights();
		services.AddDurableTaskClient();

		// repo definitions
		services.AddSingleton<IProjectRepo, ProjectRepo>();
		services.AddSingleton<ITeamMemberRepo, TeamMemberRepo>();
		services.AddSingleton<IUserRepo, UserRepo>();
		services.AddSingleton<ICurveDistributionRepo, CurveDistributionRepo>();
		services.AddSingleton<IRangeDistributionRepo, RangeDistributionRepo>();

		//service definitions
		services.AddTransient<ISecurityService, SecurityService>();
		services.AddScoped<IUserService, UserService>();
		services.AddScoped<IConfigurationService, ConfigurationService>();
		services.AddScoped<ITeamMemberService, TeamMemberService>();
		services.AddScoped<IProjectService, ProjectService>();
		services.AddScoped<ITimeCardService, TimeCardService>();

		var options = new QueueClientOptions
		{
			MessageEncoding = QueueMessageEncoding.Base64
		};
		services.AddSingleton(sp => new Azure.Storage.Queues.QueueClient(
			System.Environment.GetEnvironmentVariable("AzureWebJobsStorage", EnvironmentVariableTarget.Process),
			System.Environment.GetEnvironmentVariable("ROLLBACK_STORAGE_QUEUE_NAME", EnvironmentVariableTarget.Process),
			options
		));

		//// configure json serialization
		//services.Configure<JsonSerializerOptions>(options =>
		//{
		//    options.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
		//});
	})
	.ConfigureLogging(logging =>
	{
		logging.Services.Configure<LoggerFilterOptions>(options =>
		{
			var defaultRule = options.Rules.FirstOrDefault(
				rule => rule.ProviderName == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider"
			);

			if (defaultRule is not null)
			{
				options.Rules.Remove(defaultRule);
			}
		});
	})
	.Build();

host.Run();
