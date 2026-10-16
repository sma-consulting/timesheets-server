using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal interface ISecurityService
	{

		string WhoAmI();
		public bool AuthorizeUser();
		string GetCurrentEmail();
		bool IsCurrentUserAdmin();

		// The same, for Claude's MCP tools: their calls aren't website requests,
		// so they pass on the caller's x-ms-client-principal header themselves.
		string GetEmail(string principalHeader);
		bool IsAdmin(string principalHeader);

		string GetCurrentTeamMemberId();
		string GetCurrentUserId();

	}
}
