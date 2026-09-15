using MongoDB.Driver;
using System;
using System.Collections.Generic;

namespace sma.plan
{
	internal interface IDatabaseRepo<T> where T : GeneralModel
	{
		MongoCollectionBase<T> DBCollection { get; set; }

		T Create(T data);
		T Delete(string id);
		T Get(string id);
		List<T> GetAll();
		Tuple<T, T> Update(T data);
	}
}