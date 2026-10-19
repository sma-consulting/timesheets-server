
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



	}
}
