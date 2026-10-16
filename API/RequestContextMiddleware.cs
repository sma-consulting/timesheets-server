using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace sma.plan
{
	// The HTTP request the current function call is handling. In the isolated
	// worker model the request belongs to the function's own context, not to
	// ASP.NET's IHttpContextAccessor, so the middleware below hands it over
	// before each function runs. One per call (scoped).
	internal class RequestContext
	{
		public HttpContext HttpContext { get; set; }

		// For a Claude (MCP) tool call, which isn't a website request: the
		// x-ms-client-principal header Easy Auth added to it once the caller
		// signed in. SecurityService reads it when there's no HttpContext, so
		// every service knows who's calling, as it does for the website.
		public string McpPrincipalHeader { get; set; }
	}

	// Runs before every function. This is also where a deny-by-default
	// security check can go: it sees every request before any endpoint does.
	internal class RequestContextMiddleware : IFunctionsWorkerMiddleware
	{
		private const string McpToolTrigger = "mcpToolTrigger";
		private const string PrincipalHeader = "x-ms-client-principal";

		public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
		{
			RequestContext request = context.InstanceServices.GetRequiredService<RequestContext>();
			request.HttpContext = context.GetHttpContext();

			if (request.HttpContext == null)
			{
				request.McpPrincipalHeader = await McpPrincipalHeaderOf(context);
			}

			await next(context);
		}

		// A tool call's sign-in header, from the headers Claude's request came
		// with. Null for anything that isn't a tool call, or when there's none -
		// which SecurityService treats as nobody.
		private static async Task<string> McpPrincipalHeaderOf(FunctionContext context)
		{
			BindingMetadata trigger = context.FunctionDefinition.InputBindings.Values
				.FirstOrDefault(b => string.Equals(b.Type, McpToolTrigger, StringComparison.OrdinalIgnoreCase));
			if (trigger == null)
			{
				return null;
			}

			try
			{
				ToolInvocationContext tool = (await context.BindInputAsync<ToolInvocationContext>(trigger))?.Value;
				if (tool == null || !tool.TryGetHttpTransport(out HttpTransport transport) || transport.Headers == null)
				{
					return null;
				}

				return transport.Headers
					.FirstOrDefault(h => string.Equals(h.Key, PrincipalHeader, StringComparison.OrdinalIgnoreCase))
					.Value;
			}
			catch (Exception)
			{
				// Couldn't read the call: nobody, so admin-only tools refuse.
				return null;
			}
		}
	}
}
