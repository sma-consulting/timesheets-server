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
    }
}
