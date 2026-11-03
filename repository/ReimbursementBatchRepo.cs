using MongoDB.Driver;
using System.Collections.Generic;

namespace sma.plan
{
	internal class ReimbursementBatchRepo : DatabaseRepo<ReimbursementBatch>, IReimbursementBatchRepo
	{
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
