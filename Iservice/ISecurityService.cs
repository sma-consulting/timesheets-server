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
		string GetCurrentEmail();
		bool IsCurrentUserAdmin();

		string GetCurrentTeamMemberId();
		string GetCurrentUserId();

		// Sandbox only: the "Testing as" switch, and the role picked in it
		// (SandboxRoles), or null. See SecurityService.
		bool RoleSwitchEnabled();
		string TestRole();

	}
}
