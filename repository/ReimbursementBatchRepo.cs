using MongoDB.Driver;
using System.Collections.Generic;

namespace sma.plan
{
	internal class ReimbursementBatchRepo : DatabaseRepo<ReimbursementBatch>, IReimbursementBatchRepo
	{
		public ReimbursementBatch Find(string id)
		{
			return string.IsNullOrWhiteSpace(id)
				? null
				: DBCollection.Find(b => b.Id == id).FirstOrDefault();
		}

		public List<ReimbursementBatch> GetWithoutExpenses()
		{
			var withoutExpenses = Builders<ReimbursementBatch>.Projection
				.Exclude(nameof(ReimbursementBatch.People) + "." + nameof(ReimbursementPerson.Expenses));
			return DBCollection.Find(Builders<ReimbursementBatch>.Filter.Empty)
				.Project<ReimbursementBatch>(withoutExpenses)
				.ToList();
		}
	}
}
