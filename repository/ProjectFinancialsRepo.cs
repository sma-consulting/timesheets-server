using MongoDB.Driver;
using System.Collections.Generic;
using System.Linq;

namespace sma.plan
{
	internal class ProjectFinancialsRepo : DatabaseRepo<ProjectFinancials>, IProjectFinancialsRepo
	{
		// The financial fields still on Project documents from before they
		// moved here - read only, to copy them across.
		private readonly MongoCollectionBase<LegacyProjectFinancials> _legacy =
			Database.GetCollection<LegacyProjectFinancials>(nameof(Project));

		// The project's financials, or null if it has none yet.
		private ProjectFinancials GetByProject(string projectId)
		{
			var filter = Builders<ProjectFinancials>.Filter.Eq(f => f.ProjectId, projectId);
			return DBCollection.Find(filter).FirstOrDefault();
		}

		public List<ProjectFinancials> GetByProjects(IEnumerable<string> projectIds)
		{
			var filter = Builders<ProjectFinancials>.Filter.In(f => f.ProjectId, ValidIds(projectIds));
			return DBCollection.Find(filter).SortBy(f => f.Id).ToList();
		}

		public ProjectFinancials Save(ProjectFinancials financials)
		{
			ProjectFinancials stored = GetByProject(financials.ProjectId);
			if (stored == null)
			{
				financials.Id = null;
				return Create(financials);
			}
			financials.Id = stored.Id;
			return Update(financials).Item2;
		}

		public void DeleteByProject(string projectId)
		{
			var filter = Builders<ProjectFinancials>.Filter.Eq(f => f.ProjectId, projectId);
			DBCollection.DeleteMany(filter);
		}

		public void EnsureFromLegacy(string projectId)
		{
			if (string.IsNullOrWhiteSpace(projectId) || GetByProject(projectId) != null)
			{
				return;
			}
			LegacyProjectFinancials legacy = _legacy.Find(p => p.Id == projectId).FirstOrDefault();
			if (legacy != null && legacy.HasAny())
			{
				Create(ProjectFinancials.FromLegacy(legacy));
			}
		}

		// Reads only which projects have financials already, then only the
		// projects that don't.
		public void EnsureAllFromLegacy()
		{
			var projectIdOnly = Builders<ProjectFinancials>.Projection.Include(f => f.ProjectId);
			List<string> have = DBCollection.Find(FilterDefinition<ProjectFinancials>.Empty)
				.Project<ProjectFinancials>(projectIdOnly)
				.ToList()
				.Select(f => f.ProjectId)
				.ToList();

			var without = Builders<LegacyProjectFinancials>.Filter.Nin(p => p.Id, ValidIds(have));
			foreach (LegacyProjectFinancials legacy in _legacy.Find(without).ToList())
			{
				if (legacy.HasAny())
				{
					Create(ProjectFinancials.FromLegacy(legacy));
				}
			}
		}
	}
}
