using MongoDB.Driver;

namespace sma.plan
{
	internal class ProjectRoleRepo : DatabaseRepo<ProjectRole>, IProjectRoleRepo
	{
		public bool NameTaken(string name, string exceptId)
		{
			var filter = TextIs(r => r.Name, name);
			if (exceptId != null)
			{
				filter &= Builders<ProjectRole>.Filter.Ne(r => r.Id, exceptId);
			}
			return DBCollection.CountDocuments(filter, new CountOptions { Limit = 1 }) > 0;
		}

		public ProjectRole GetProjectManager()
		{
			var filter = TextIs(r => r.Code, "PM") | TextIs(r => r.Name, "Project Manager");
			return DBCollection.Find(filter).SortBy(r => r.Id).FirstOrDefault();
		}
	}
}
