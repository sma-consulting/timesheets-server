using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
    internal class DatabaseRepo<T> : IDatabaseRepo<T> where T : GeneralModel
    {
        public MongoCollectionBase<T> DBCollection { get; set; } = Database.GetCollection<T>();

        public T Create(T data)
        {
            DBCollection.InsertOne(data);
            return Get(data.Id);
        }

        public T Delete(string id)
        {
            T deleted = Get(id);
            DBCollection.DeleteOne(d => d.Id == id);
            return deleted;
        }

        public T Get(string id)
        {
            return DBCollection.Find(d => d.Id == id).First();
        }

        public Tuple<T, T> Update(T data)
        {
            T deleted = Get(data.Id);
            DBCollection.ReplaceOne(d => d.Id ==  data.Id, data);
            return new Tuple<T, T>(deleted, Get(data.Id));
        }

        public List<T> GetAll()
        {
            return DBCollection.Find(_ => true).ToList();
        }

        // Whether a document with the id exists - asks the database, loads nothing.
        public bool Exists(string id)
        {
            return ObjectId.TryParse(id, out _)
                && DBCollection.CountDocuments(d => d.Id == id, new CountOptions { Limit = 1 }) > 0;
        }

        // Ids that aren't ObjectIds can't match anything, and would throw in a filter.
        protected static List<string> ValidIds(IEnumerable<string> ids)
        {
            return (ids ?? Enumerable.Empty<string>())
                .Where(id => ObjectId.TryParse(id, out _))
                .ToList();
        }

        // Past this many months a range is narrowed by year only (see DateBetween).
        private const int MaxMonthsInDateFilter = 24;

        // A filter for a DateOnly `field` between `from` and `to` (inclusive; a
        // null bound leaves that side open). Dates are stored as { Year, Month,
        // Day } documents, which the database isn't relied on to compare as a
        // whole, so the range is asked for month by month on those parts: each
        // month's Year and Month, with Day bounded in the first and last. A long
        // or open range is narrowed by year only. Either way the query can
        // return a little more than the range, never less - callers check the
        // exact range again in C#.
        protected static FilterDefinition<T> DateBetween(string field, DateOnly? from, DateOnly? to)
        {
            var f = Builders<T>.Filter;
            string year = field + ".Year", month = field + ".Month", day = field + ".Day";

            if (from == null && to == null)
            {
                return f.Empty;
            }
            if (from == null || to == null)
            {
                return from != null ? f.Gte(year, from.Value.Year) : f.Lte(year, to.Value.Year);
            }

            DateOnly first = from.Value, last = to.Value;
            int months = (last.Year - first.Year) * 12 + last.Month - first.Month + 1;
            if (months > MaxMonthsInDateFilter)
            {
                return f.Gte(year, first.Year) & f.Lte(year, last.Year);
            }

            var eachMonth = new List<FilterDefinition<T>>();
            for (DateOnly m = new DateOnly(first.Year, first.Month, 1); m <= last; m = m.AddMonths(1))
            {
                var inMonth = f.Eq(year, m.Year) & f.Eq(month, m.Month);
                if (m.Year == first.Year && m.Month == first.Month)
                {
                    inMonth &= f.Gte(day, first.Day);
                }
                if (m.Year == last.Year && m.Month == last.Month)
                {
                    inMonth &= f.Lte(day, last.Day);
                }
                eachMonth.Add(inMonth);
            }
            return f.Or(eachMonth);
        }
    }
}
