using MongoDB.Driver;
using System;
using System.Linq.Expressions;

namespace sma.plan
{
	// Indexes on the fields our queries filter by. The MongoDB API only indexes
	// _id by default, so without these a filtered query still reads the whole
	// collection on the server. Creating an index that already exists does
	// nothing, so this runs at every startup and only does work the first time
	// it meets a database (local, sandbox, production).
	//
	// Only collections that grow are indexed. Users, roles, customers and the
	// like stay small enough that reading them all costs nothing, and every
	// index adds a little to each write.
	internal static class DatabaseIndexes
	{
		public static void Ensure(Action<string, Exception> warn)
		{
			// The timesheet and PM views, invoice entries, and the task and
			// sub-task delete checks.
			On<TimeEntry>(warn, e => e.TeamMemberId, e => e.ProjectId,
				e => e.ProjectTaskId, e => e.ProjectSubTaskId);

			// "My expenses" and the PM expense report.
			On<Expense>(warn, e => e.TeamMemberId, e => e.ProjectId);

			// Which projects someone manages - checked on most PM requests.
			On<Project>(warn, p => p.ProjectManagerId);

			// A span of days: Staff hours' month, the Time Log's week, a report's
			// period, the Invoices page's last 12 months.
			OnDate<TimeEntry>(warn, nameof(TimeEntry.Date));
			OnDate<Expense>(warn, nameof(Expense.Date));
			OnDate<Invoice>(warn, nameof(Invoice.Month));
		}

		// Dates are stored as { Year, Month, Day } and asked for on those parts
		// (DatabaseRepo.DateBetween), so one index over the three, in that order.
		private static void OnDate<T>(Action<string, Exception> warn, string field)
			where T : GeneralModel
		{
			try
			{
				Database.GetCollection<T>().Indexes.CreateOne(new CreateIndexModel<T>(
					Builders<T>.IndexKeys
						.Ascending(field + ".Year")
						.Ascending(field + ".Month")
						.Ascending(field + ".Day")));
			}
			catch (Exception ex)
			{
				warn(typeof(T).Name + "." + field, ex);
			}
		}

		// One index per field, each created on its own so that one failing
		// doesn't stop the rest. Queries still work without an index, just slower.
		private static void On<T>(Action<string, Exception> warn, params Expression<Func<T, object>>[] fields)
			where T : GeneralModel
		{
			var collection = Database.GetCollection<T>();
			foreach (var field in fields)
			{
				try
				{
					collection.Indexes.CreateOne(new CreateIndexModel<T>(
						Builders<T>.IndexKeys.Ascending(field)));
				}
				catch (Exception ex)
				{
					warn(typeof(T).Name + "." + FieldName(field), ex);
				}
			}
		}

		// "TeamMemberId" from e => e.TeamMemberId, for the log.
		private static string FieldName<T>(Expression<Func<T, object>> field)
		{
			Expression body = field.Body is UnaryExpression convert ? convert.Operand : field.Body;
			return body is MemberExpression member ? member.Member.Name : field.ToString();
		}
	}
}
