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
    }
}
