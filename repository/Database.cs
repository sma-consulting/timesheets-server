using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
    internal class Database
    {
        private static readonly Lazy<Database> lazy = new (() => new Database());

        private readonly MongoDatabaseBase database;

        private Database() 
        {
            var connectionString = System.Environment.GetEnvironmentVariable("DATABASE_CONNECTION", EnvironmentVariableTarget.Process);
            var connectionDB = System.Environment.GetEnvironmentVariable("DATABASE_NAME", EnvironmentVariableTarget.Process);

            var mongoClient = new MongoClient(connectionString);
            database = (MongoDatabaseBase)mongoClient.GetDatabase(connectionDB);
        }

        private static MongoDatabaseBase DB { get { return lazy.Value.database; } }

        public static MongoCollectionBase<T> GetCollection<T>() where T : GeneralModel
        {
            return (MongoCollectionBase<T>)DB.GetCollection<T>(typeof(T).Name);
        }
    }
}
