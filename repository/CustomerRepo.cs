using MongoDB.Driver;
using System.Collections.Generic;

namespace sma.plan
{
	internal class CustomerRepo : DatabaseRepo<Customer>, ICustomerRepo
	{
		public List<Customer> FindByName(string text)
		{
			var idAndName = Builders<Customer>.Projection.Include(c => c.Name);
			return DBCollection.Find(TextContains(c => c.Name, text))
				.Project<Customer>(idAndName)
				.SortBy(c => c.Id)
				.ToList();
		}

		public Customer GetByName(string name)
		{
			return DBCollection.Find(TextIs(c => c.Name, name)).SortBy(c => c.Id).FirstOrDefault();
		}
	}
}
