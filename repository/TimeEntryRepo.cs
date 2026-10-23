
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class TimeEntryRepo : DatabaseRepo<TimeEntry>, ITimeEntryRepo
	{
		// In one write each, rather than entry by entry: an invoice bills a
		// month of everyone's time.
		public void MarkInvoiced(IEnumerable<string> timeEntryIds, string invoiceId)
		{
			List<string> ids = (timeEntryIds ?? Enumerable.Empty<string>())
				.Where(id => ObjectId.TryParse(id, out _))
				.Distinct()
				.ToList();
			if (ids.Count == 0 || string.IsNullOrWhiteSpace(invoiceId))
			{
				return;
			}

			var filter = Builders<TimeEntry>.Filter.In(e => e.Id, ids)
				& Builders<TimeEntry>.Filter.Ne(e => e.InvoiceId, invoiceId);
			var update = Builders<TimeEntry>.Update
				.Set(e => e.Invoiced, true)
				.Set(e => e.InvoiceId, invoiceId);
			DBCollection.UpdateMany(filter, update);
		}

		public void ClearInvoiced(string invoiceId)
		{
			if (string.IsNullOrWhiteSpace(invoiceId))
			{
				return;
			}

			var filter = Builders<TimeEntry>.Filter.Eq(e => e.InvoiceId, invoiceId);
			var update = Builders<TimeEntry>.Update
				.Set(e => e.Invoiced, false)
				.Set(e => e.InvoiceId, (string)null);
			DBCollection.UpdateMany(filter, update);
		}

		public long CountInvoiced()
		{
			return DBCollection.CountDocuments(Builders<TimeEntry>.Filter.Eq(e => e.Invoiced, true));
		}

		// Filtered in the database, so only these entries are loaded rather than
		// the whole collection.
		public List<TimeEntry> GetVisibleTo(string teamMemberId, IEnumerable<string> projectIds)
		{
			// Ids that aren't ObjectIds can't match anything, and would throw in a filter.
			List<string> projects = (projectIds ?? Enumerable.Empty<string>())
				.Where(id => ObjectId.TryParse(id, out _))
				.ToList();

			var filter = Builders<TimeEntry>.Filter.Eq(e => e.TeamMemberId, teamMemberId)
				| Builders<TimeEntry>.Filter.In(e => e.ProjectId, projects);

			// Id order: the order GetAll returned them in.
			return DBCollection.Find(filter).SortBy(e => e.Id).ToList();
		}

		public List<TimeEntry> GetFiltered(string teamMemberId, string projectId, DateOnly? from, DateOnly? to,
			string visibleTo, IEnumerable<string> visibleProjectIds)
		{
			var f = Builders<TimeEntry>.Filter;
			var filter = DateBetween(nameof(TimeEntry.Date), from, to);

			foreach ((string id, bool isMember) in new[] { (teamMemberId, true), (projectId, false) })
			{
				if (id == null)
				{
					continue;
				}
				// An id that isn't an ObjectId can't match, and would throw in a filter.
				if (!ObjectId.TryParse(id, out _))
				{
					return new List<TimeEntry>();
				}
				filter &= isMember ? f.Eq(e => e.TeamMemberId, id) : f.Eq(e => e.ProjectId, id);
			}

			if (visibleTo != null)
			{
				filter &= f.Eq(e => e.TeamMemberId, visibleTo) | f.In(e => e.ProjectId, ValidIds(visibleProjectIds));
			}

			// The pages don't use the history, and it can be most of an entry.
			var withoutHistory = Builders<TimeEntry>.Projection.Exclude(e => e.History);

			// The exact range: the query narrows by month (see DateBetween).
			return DBCollection.Find(filter).SortBy(e => e.Id).Project<TimeEntry>(withoutHistory).ToList()
				.Where(e => (from == null || e.Date >= from.Value) && (to == null || e.Date <= to.Value))
				.ToList();
		}

				// Counted by the database: nothing is loaded at all.
		public long CountLoggedAgainst(IEnumerable<string> projectTaskIds, IEnumerable<string> projectSubTaskIds)
		{
			List<string> tasks = (projectTaskIds ?? Enumerable.Empty<string>())
				.Where(id => ObjectId.TryParse(id, out _))
				.ToList();
			List<string> subTasks = (projectSubTaskIds ?? Enumerable.Empty<string>())
				.Where(id => ObjectId.TryParse(id, out _))
				.ToList();

			var filter = Builders<TimeEntry>.Filter.In(e => e.ProjectTaskId, tasks)
				| Builders<TimeEntry>.Filter.In(e => e.ProjectSubTaskId, subTasks);

			return DBCollection.CountDocuments(filter);
		}

		public List<TimeEntry> GetByProject(string projectId)
		{
			if (!ObjectId.TryParse(projectId, out _))
			{
				return new List<TimeEntry>();
			}

			var filter = Builders<TimeEntry>.Filter.Eq(e => e.ProjectId, projectId);
			return DBCollection.Find(filter).SortBy(e => e.Id).ToList();
		}

		public List<TimeEntry> GetHoursBetween(DateOnly from, DateOnly to)
		{
			var hoursOnly = Builders<TimeEntry>.Projection
				.Include(e => e.TeamMemberId)
				.Include(e => e.ProjectId)
				.Include(e => e.ProjectSubTaskId)
				.Include(e => e.Date)
				.Include(e => e.Hours);

			// The exact range: the query narrows by month (see DateBetween).
			return DBCollection.Find(DateBetween(nameof(TimeEntry.Date), from, to))
				.Project<TimeEntry>(hoursOnly)
				.ToList()
				.Where(e => e.Date >= from && e.Date <= to)
				.ToList();
		}



	}
}
