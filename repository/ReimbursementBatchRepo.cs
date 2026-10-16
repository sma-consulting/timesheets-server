using MongoDB.Driver;

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
	}
}
