using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan { 

	// Shape of the "x-ms-client-principal" header Azure Easy Auth injects once a
	// request has been through a real login. Never present on localhost.
	internal class ClientPrincipalDto
	{
		public string IdentityProvider { get; set; }
		public string UserId { get; set; }
		public string UserDetails { get; set; }
		public IEnumerable<string> UserRoles { get; set; }
		public IEnumerable<ClientClaim> Claims { get; set; }
	}

	internal class ClientClaim
	{
		public string Typ { get; set; }
		public string Val { get; set; }
	}

	internal class SecurityService : ISecurityService
	{
		private const string EmailUriClaim = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress";

		private ClaimsPrincipal _principal;
		private HttpContext _httpContext;

		public SecurityService(IHttpContextAccessor httpContext)
		{
			_principal = httpContext.HttpContext.User;
			_httpContext = httpContext.HttpContext;
		}

		// Easy Auth never runs on localhost, so there is no header to decode while
		// developing - LOCAL_TEST_EMAIL stands in for a real login so that requests
		// resolve to an actual user record instead of "ANONYMOUS".
		public string GetCurrentEmail()
		{
			if (System.Diagnostics.Debugger.IsAttached)
			{
				return Environment.GetEnvironmentVariable("LOCAL_TEST_EMAIL");
			}

			if (_httpContext == null ||
				!_httpContext.Request.Headers.TryGetValue("x-ms-client-principal", out var headerValues))
			{
				return null;
			}

			try
			{
				var json = Encoding.UTF8.GetString(Convert.FromBase64String(headerValues.First()));
				var principal = JsonConvert.DeserializeObject<ClientPrincipalDto>(json);

				if (principal?.Claims == null)
				{
					return null;
				}

				foreach (var claim in principal.Claims)
				{
					if (string.IsNullOrEmpty(claim.Typ) || string.IsNullOrEmpty(claim.Val))
					{
						continue;
					}

					string type = claim.Typ.ToLower();

					if (type == "email" || type == "upn" || type == "preferred_username" || type == EmailUriClaim)
					{
						return claim.Val;
					}
				}

				return null;
			}
			catch (Exception)
			{
				return null;
			}
		}

		public string WhoAmI()
		{
			return _principal.GetUserName();
		}

		public bool AuthorizeUser()
		{
			return true;
		}
	}
}
