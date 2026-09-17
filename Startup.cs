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

			//service definitions
			builder.Services.AddTransient<ISecurityService, SecurityService>();
			builder.Services.AddScoped<IUserService, UserService>();
			builder.Services.AddScoped<IConfigurationService, ConfigurationService>();
			builder.Services.AddScoped<ITeamMemberService, TeamMemberService>();
			builder.Services.AddScoped<IProjectService, ProjectService>();
			builder.Services.AddScoped<ITimeEntryService, TimeEntryService>();
			builder.Services.AddScoped<IProjectTaskService, ProjectTaskService>();
			builder.Services.AddScoped<IProjectSubTaskService, ProjectSubTaskService>();



		}
	}
}
