
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class ProjectRepo : DatabaseRepo<Project>, IProjectRepo
	{
		public List<Project> GetByIds(IEnumerable<string> ids)
		{
			var filter = Builders<Project>.Filter.In(p => p.Id, ValidIds(ids));
			return DBCollection.Find(filter).SortBy(p => p.Id).ToList();
		}

		public HashSet<string> IdsManagedBy(string teamMemberId)
		{
			var filter = Builders<Project>.Filter.In(p => p.ProjectManagerId, ValidIds(new[] { teamMemberId }));
			return DBCollection.Find(filter).ToList().Select(p => p.Id).ToHashSet();
		}

		public long CountByCustomer(string customerId)
		{
			var filter = Builders<Project>.Filter.In(p => p.CustomerId, ValidIds(new[] { customerId }));
			return DBCollection.CountDocuments(filter);
		}
	}
}
