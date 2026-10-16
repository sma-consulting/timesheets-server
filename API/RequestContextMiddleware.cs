using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
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
	}

	// Runs before every function. This is also where a deny-by-default
	// security check can go: it sees every request before any endpoint does.
	internal class RequestContextMiddleware : IFunctionsWorkerMiddleware
	{
		public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
		{
			context.InstanceServices.GetRequiredService<RequestContext>().HttpContext =
				context.GetHttpContext();

			await next(context);
		}
	}
}
