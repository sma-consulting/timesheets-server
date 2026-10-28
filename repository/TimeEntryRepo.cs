
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

		// One update in the database: nothing is read first. Exact only for ranges
		// DateBetween filters day by day (24 months or less) - the endpoint keeps
		// to that.
		public long SetLocked(string teamMemberId, string projectId, DateOnly from, DateOnly to,
			DateTime? lockedAt, string lockedBy)
		{
			var f = Builders<TimeEntry>.Filter;
			// Locking skips what's already locked, unlocking what isn't. Entries from
			// before the lock have no LockedAt at all, which Eq null also matches.
			var filter = DateBetween(nameof(TimeEntry.Date), from, to)
				& (lockedAt != null ? f.Eq(e => e.LockedAt, null) : f.Ne(e => e.LockedAt, null));

			foreach ((string id, bool isMember) in new[] { (teamMemberId, true), (projectId, false) })
			{
				if (id == null)
				{
					continue;
				}
				// An id that isn't an ObjectId can't match, and would throw in a filter.
				if (!ObjectId.TryParse(id, out _))
				{
					return 0;
				}
				filter &= isMember ? f.Eq(e => e.TeamMemberId, id) : f.Eq(e => e.ProjectId, id);
			}

			var update = Builders<TimeEntry>.Update
				.Set(e => e.LockedAt, lockedAt)
				.Set(e => e.LockedBy, lockedBy);
			return DBCollection.UpdateMany(filter, update).ModifiedCount;
		}

		public long CountInvoiced()
		{
			return DBCollection.CountDocuments(Builders<TimeEntry>.Filter.Eq(e => e.Invoiced, true));
		}

		// A save is one call to the database: unlike Create, Update and Delete,
		// these don't read the entry before or after.
		public TimeEntry Find(string id)
		{
			if (!ObjectId.TryParse(id, out _))
			{
				return null;
			}
			return DBCollection.Find(e => e.Id == id).FirstOrDefault();
		}

		public TimeEntry Insert(TimeEntry data)
		{
			DBCollection.InsertOne(data);
			return data;
		}

		public TimeEntry Replace(TimeEntry data)
		{
			DBCollection.ReplaceOne(e => e.Id == data.Id, data);
			return data;
		}

		public void Remove(string id)
		{
			DBCollection.DeleteOne(e => e.Id == id);
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

		public List<TimeEntry> GetOnTasks(IEnumerable<string> projectTaskIds, DateOnly from, DateOnly to)
		{
			var filter = DateBetween(nameof(TimeEntry.Date), from, to)
				& Builders<TimeEntry>.Filter.In(e => e.ProjectTaskId, ValidIds(projectTaskIds));
			var withoutHistory = Builders<TimeEntry>.Projection.Exclude(e => e.History);

			// The exact range: the query narrows by month (see DateBetween).
			return DBCollection.Find(filter).Project<TimeEntry>(withoutHistory).ToList()
				.Where(e => e.Date >= from && e.Date <= to)
				.ToList();
		}

		public List<TimeEntry> GetHoursBetween(DateOnly from, DateOnly to)
		{
			var hoursOnly = Builders<TimeEntry>.Projection
				.Include(e => e.TeamMemberId)
				.Include(e => e.ProjectId)
				.Include(e => e.ProjectSubTaskId)
				.Include(e => e.Date)
				.Include(e => e.Hours)
				.Include(e => e.Invoiced)
				.Include(e => e.LockedAt);

			// The exact range: the query narrows by month (see DateBetween).
			return DBCollection.Find(DateBetween(nameof(TimeEntry.Date), from, to))
				.Project<TimeEntry>(hoursOnly)
				.ToList()
				.Where(e => e.Date >= from && e.Date <= to)
				.ToList();
		}



	}
}
