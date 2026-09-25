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
		private IUserRepo _userRepo;
		private ITeamMemberRepo _teamMemberRepo;

		public SecurityService(IHttpContextAccessor httpContext, IUserRepo userRepo, ITeamMemberRepo teamMemberRepo)
		{
			_principal = httpContext.HttpContext.User;
			_httpContext = httpContext.HttpContext;
			_userRepo = userRepo;
			_teamMemberRepo = teamMemberRepo;
		}

		// The TeamMember representing the caller - what time entries are owned by.
		// Null when nobody can be identified, or when the User has no TeamMember.
		public string GetCurrentTeamMemberId()
		{
			var email = GetCurrentEmail();

			if (string.IsNullOrWhiteSpace(email))
			{
				return null;
			}

			User user = _userRepo.GetByEmail(email.Trim());

			if (user == null)
			{
				return null;
			}

			return _teamMemberRepo.GetByUserId(user.Id)?.Id;
		}

		// Admin is a flag on the User record, resolved from whoever the current
		// request belongs to. Returns false when nobody can be identified.
		public bool IsCurrentUserAdmin()
		{
			var email = GetCurrentEmail();

			if (string.IsNullOrWhiteSpace(email))
			{
				return false;
			}

			User user = _userRepo.GetByEmail(email.Trim());

			// Compared case-insensitively on purpose: privileges are typed into
			// Mongo by hand, and "Admin" silently failing to grant admin is a very
			// expensive typo to debug.
			return user != null && user.Privileges != null &&
				user.Privileges.Any(p =>
					string.Equals(p?.Trim(), User.AdminPrivilege, StringComparison.OrdinalIgnoreCase));
		}

		// Easy Auth never runs on localhost, so there is no header to decode while
		// developing - LOCAL_TEST_EMAIL stands in for a real login so that requests
		// resolve to an actual user record instead of "ANONYMOUS". An x-test-email
		// header overrides it, so the login screen can switch user without a
		// restart.
		//
		// This branch is reachable only when LOCAL_TEST_EMAIL is set (or a debugger
		// is attached). That setting lives solely in local.settings.json, which is
		// never deployed, so in production the header is never read at all.
		// NEVER set LOCAL_TEST_EMAIL in Azure - it would let anyone impersonate
		// anyone by sending a header.
		public string GetCurrentEmail()
		{
			var localTestEmail = Environment.GetEnvironmentVariable("LOCAL_TEST_EMAIL");

			if (System.Diagnostics.Debugger.IsAttached || !string.IsNullOrEmpty(localTestEmail))
			{
				var fromHeader = _httpContext?.Request.Headers["x-test-email"].FirstOrDefault();
				return string.IsNullOrWhiteSpace(fromHeader) ? localTestEmail : fromHeader;
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
