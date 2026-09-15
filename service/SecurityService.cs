using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan { 

	internal class SecurityService : ISecurityService
	{
		private ClaimsPrincipal _principal;

		public SecurityService(IHttpContextAccessor httpContext)
		{
			_principal = httpContext.HttpContext.User;
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
