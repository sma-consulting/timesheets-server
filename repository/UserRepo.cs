using MongoDB.Driver;
using sma.plan;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class UserRepo: DatabaseRepo<User>, IUserRepo
	{
		public User GetByEmail(string email)
		{
			return DBCollection.Find(u => u.Email == email).FirstOrDefault();
		}
	}
}
