using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using sma.plan;

// The isolated worker's entry point: builds the host that runs the functions
// in their own process. ASP.NET Core integration keeps HttpRequest and
// IActionResult working as they did in-process.
var host = new HostBuilder()
	.ConfigureFunctionsWebApplication(worker =>
	{
		// In this order: hand each call's HTTP request to SecurityService,
		// then refuse it unless the function's [Allow] role is met.
		worker.UseMiddleware<RequestContextMiddleware>();
		worker.UseMiddleware<SecurityMiddleware>();
	})
	.ConfigureServices(services =>
	{
		// Responses serialized with Newtonsoft, as in-process, so the
		// frontend sees exactly the same JSON.
		services.AddMvc().AddNewtonsoftJson();

		services.AddScoped<RequestContext>();

		// repo definitions
		services.AddSingleton<IProjectRepo, ProjectRepo>();
		services.AddSingleton<ITeamMemberRepo, TeamMemberRepo>();
		services.AddSingleton<IUserRepo, UserRepo>();
		services.AddSingleton<ICurveDistributionRepo, CurveDistributionRepo>();
		services.AddSingleton<IRangeDistributionRepo, RangeDistributionRepo>();
		services.AddSingleton<ITimeEntryRepo, TimeEntryRepo>();
		services.AddSingleton<IProjectTaskRepo, ProjectTaskRepo>();
		services.AddSingleton<IProjectSubTaskRepo, ProjectSubTaskRepo>();
		services.AddSingleton<IProjectAssignmentRepo, ProjectAssignmentRepo>();
		services.AddSingleton<ICustomerRepo, CustomerRepo>();
		services.AddSingleton<IProjectBillingRateRepo, ProjectBillingRateRepo>();
		services.AddSingleton<IExpenseRepo, ExpenseRepo>();
		services.AddSingleton<IExpenseReceiptRepo, ExpenseReceiptRepo>();

		// Receipt files. Locally a connection string (Azurite) or the storage
		// URL with your own Azure login; in Azure the account URL plus the
		// Function App's managed identity, so no key is stored anywhere.
		services.AddSingleton(sp =>
		{
			string connection = Environment.GetEnvironmentVariable("RECEIPT_STORAGE_CONNECTION");
			string url = Environment.GetEnvironmentVariable("RECEIPT_STORAGE_URL");
			string container = Environment.GetEnvironmentVariable("RECEIPT_CONTAINER_NAME") ?? "expense-receipts";

			BlobServiceClient service;
			if (!string.IsNullOrWhiteSpace(connection))
			{
				service = new BlobServiceClient(connection);
			}
			else if (!string.IsNullOrWhiteSpace(url))
			{
				service = new BlobServiceClient(new Uri(url), new DefaultAzureCredential());
			}
			else
			{
				throw new InvalidOperationException(
					"Set RECEIPT_STORAGE_CONNECTION or RECEIPT_STORAGE_URL so receipts can be stored.");
			}

			BlobContainerClient client = service.GetBlobContainerClient(container);
			client.CreateIfNotExists(); // private by default
			return client;
		});

		// service definitions
		services.AddTransient<ISecurityService, SecurityService>();
		services.AddScoped<IUserService, UserService>();
		services.AddScoped<IConfigurationService, ConfigurationService>();
		services.AddScoped<ITeamMemberService, TeamMemberService>();
		services.AddScoped<IProjectService, ProjectService>();
		services.AddScoped<ITimeEntryService, TimeEntryService>();
		services.AddScoped<IProjectTaskService, ProjectTaskService>();
		services.AddScoped<IProjectSubTaskService, ProjectSubTaskService>();
		services.AddScoped<IProjectAssignmentService, ProjectAssignmentService>();
	})
	.Build();

// A function without [Allow] is refused on every call; say so at startup
// rather than leaving it to be found from a failed request.
var missing = SecurityMiddleware.FunctionsWithoutAllow().ToList();
if (missing.Count > 0)
{
	host.Services.GetRequiredService<ILoggerFactory>()
		.CreateLogger("Security")
		.LogWarning("These functions have no [Allow] role and will refuse every call: {Functions}",
			string.Join(", ", missing));
}

host.Run();
