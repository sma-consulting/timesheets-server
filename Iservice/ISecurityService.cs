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

	}
}
