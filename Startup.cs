using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[assembly: FunctionsStartup(typeof(sma.plan.Startup))]

namespace sma.plan
{
	public class Startup : FunctionsStartup
	{
		public override void Configure(IFunctionsHostBuilder builder)
		{
			//platform service provider registrations
			builder.Services.AddHttpContextAccessor();
			builder.Services.AddLogging();

			// repo definitions
			builder.Services.AddSingleton<IProjectRepo, ProjectRepo>();
			builder.Services.AddSingleton<ITeamMemberRepo, TeamMemberRepo>();
			builder.Services.AddSingleton<IUserRepo, UserRepo>();
			builder.Services.AddSingleton<ICurveDistributionRepo, CurveDistributionRepo>();
			builder.Services.AddSingleton<IRangeDistributionRepo, RangeDistributionRepo>();
			builder.Services.AddSingleton<ITimeEntryRepo, TimeEntryRepo>();
			builder.Services.AddSingleton<IProjectTaskRepo, ProjectTaskRepo>();
			builder.Services.AddSingleton<IProjectSubTaskRepo, ProjectSubTaskRepo>();
			builder.Services.AddSingleton<IProjectAssignmentRepo, ProjectAssignmentRepo>();
			builder.Services.AddSingleton<ICustomerRepo, CustomerRepo>();
			builder.Services.AddSingleton<IProjectBillingRateRepo, ProjectBillingRateRepo>();
			builder.Services.AddSingleton<IExpenseRepo, ExpenseRepo>();
			builder.Services.AddSingleton<IExpenseReceiptRepo, ExpenseReceiptRepo>();

			// Receipt files. Locally a connection string (Azurite); in Azure the
			// account URL plus the Function App's managed identity, so no key is
			// stored anywhere.
			builder.Services.AddSingleton(sp =>
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

			//service definitions
			builder.Services.AddTransient<ISecurityService, SecurityService>();
			builder.Services.AddScoped<IUserService, UserService>();
			builder.Services.AddScoped<IConfigurationService, ConfigurationService>();
			builder.Services.AddScoped<ITeamMemberService, TeamMemberService>();
			builder.Services.AddScoped<IProjectService, ProjectService>();
			builder.Services.AddScoped<ITimeEntryService, TimeEntryService>();
			builder.Services.AddScoped<IProjectTaskService, ProjectTaskService>();
			builder.Services.AddScoped<IProjectSubTaskService, ProjectSubTaskService>();
			builder.Services.AddScoped<IProjectAssignmentService, ProjectAssignmentService>();



		}
	}
}
