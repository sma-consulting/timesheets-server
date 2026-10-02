using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal interface IUserService
	{
		TeamMember ResolveOrCreate(string email);
		bool IsAdmin(string email);
	}
}
