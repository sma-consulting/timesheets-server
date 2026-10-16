using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal interface IUserRepo
	{

		User Create(User data);
		User Delete(string id);
		User Get(string id);
		List<User> GetAll();
		Tuple<User, User> Update(User data);
		User GetByEmail(string email);
	}
}
