using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace sma.plan
{
	// Runs before every function and lets the call through only if the
	// function's [Allow] role is met. A function without [Allow] is refused:
	// endpoints are closed unless they say who may use them.
	//
	// Refusals use the API's usual envelope - HTTP 200 with the real code in
	// status.code - which is what the frontend expects from every endpoint.
	internal class SecurityMiddleware : IFunctionsWorkerMiddleware
	{
		// Entry point ("sma.plan.TimeEntryFunctions.RunGetTimeEntry") -> role,
		// or null when the function has no [Allow]. Looked up once per function.
		private static readonly ConcurrentDictionary<string, Role?> RequiredRoles = new();

		public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
		{
			HttpContext http = context.GetHttpContext();
			if (http == null)
			{
				// Not an HTTP call; every function here is HTTP, but don't guess.
				await next(context);
				return;
			}

			Role? required = RequiredRoles.GetOrAdd(context.FunctionDefinition.EntryPoint, RoleOf);
			ISecurityService security = context.InstanceServices.GetRequiredService<ISecurityService>();

			if (required == null)
			{
				context.GetLogger<SecurityMiddleware>().LogWarning(
					"Refused {Function}: it has no [Allow] role.", context.FunctionDefinition.Name);
				await Refuse(http, 403, "This action is not available.");
				return;
			}

			if (required != Role.Anyone &&
				string.IsNullOrWhiteSpace(security.GetCurrentTeamMemberId()))
			{
				await Refuse(http, 401, "Not signed in.");
				return;
			}

			if (required == Role.Admin && !security.IsCurrentUserAdmin())
			{
				await Refuse(http, 403, "Administrator access is required.");
				return;
			}

			await next(context);
		}

		// Functions that would be refused because they declare no role, for the
		// startup warning.
		public static IEnumerable<string> FunctionsWithoutAllow() =>
			typeof(SecurityMiddleware).Assembly.GetTypes()
				.SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
				.Where(m => m.GetCustomAttribute<FunctionAttribute>() != null &&
					m.GetCustomAttribute<AllowAttribute>() == null)
				.Select(m => m.GetCustomAttribute<FunctionAttribute>().Name);

		private static Role? RoleOf(string entryPoint)
		{
			int dot = entryPoint.LastIndexOf('.');
			if (dot < 0)
			{
				return null;
			}

			MethodInfo method = typeof(SecurityMiddleware).Assembly
				.GetType(entryPoint.Substring(0, dot))
				?.GetMethod(entryPoint.Substring(dot + 1),
					BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

			return method?.GetCustomAttribute<AllowAttribute>()?.Role;
		}

		private static Task Refuse(HttpContext http, int code, string message)
		{
			http.Response.StatusCode = StatusCodes.Status200OK;
			return http.Response.WriteAsJsonAsync(new
			{
				status = new { code, error = message },
				result = new { },
			});
		}
	}
}
