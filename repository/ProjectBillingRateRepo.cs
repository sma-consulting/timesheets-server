using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;

namespace sma.plan
{
	internal class ProjectBillingRateRepo : DatabaseRepo<ProjectBillingRate>, IProjectBillingRateRepo
	{
		public List<ProjectBillingRate> GetByList(string rateListId, IEnumerable<string> teamMemberIds = null)
		{
			var f = Builders<ProjectBillingRate>.Filter;
			var filter = f.In(r => r.RateListId, ValidIds(new[] { rateListId }));
			if (teamMemberIds != null)
			{
				filter &= f.In(r => r.TeamMemberId, ValidIds(teamMemberIds));
			}
			return DBCollection.Find(filter).ToList();
		}

		public List<ProjectBillingRate> GetByLists(IEnumerable<string> rateListIds)
		{
			var filter = Builders<ProjectBillingRate>.Filter.In(r => r.RateListId, ValidIds(rateListIds));
			return DBCollection.Find(filter).ToList();
		}

		public List<ProjectBillingRate> GetForProjects(IEnumerable<Project> projects)
		{
			List<Project> priced = projects.Where(p => !string.IsNullOrEmpty(p.RateListId)).ToList();
			if (priced.Count == 0)
			{
				return new List<ProjectBillingRate>();
			}

			// Every list once, in one read.
			var filter = Builders<ProjectBillingRate>.Filter.In(r => r.RateListId, ValidIds(priced.Select(p => p.RateListId).Distinct()));
			List<ProjectBillingRate> rows = DBCollection.Find(filter).ToList();
			return priced
				.SelectMany(p => rows
					.Where(r => r.RateListId == p.RateListId)
					.Select(r => new ProjectBillingRate
					{
						Id = r.Id,
						RateListId = r.RateListId,
						TeamMemberId = r.TeamMemberId,
						ProjectId = p.Id,
						HourlyRate = r.HourlyRate,
						StartDate = r.StartDate,
						EndDate = r.EndDate,
					}))
				.ToList();
		}

		public void CreateMany(List<ProjectBillingRate> rows)
		{
			if (rows.Count > 0)
			{
				DBCollection.InsertMany(rows);
			}
		}

		// One update for all of them; a row already ended is left as it is.
		public long Close(IEnumerable<string> ids, DateOnly endDate)
		{
			var f = Builders<ProjectBillingRate>.Filter;
			var filter = f.In(r => r.Id, ValidIds(ids)) & f.Eq(r => r.EndDate, null);
			return DBCollection.UpdateMany(filter, Builders<ProjectBillingRate>.Update.Set(r => r.EndDate, (DateOnly?)endDate)).ModifiedCount;
		}

		public long DeleteByList(string rateListId)
		{
			var filter = Builders<ProjectBillingRate>.Filter.In(r => r.RateListId, ValidIds(new[] { rateListId }));
			return DBCollection.DeleteMany(filter).DeletedCount;
		}
	}
}
