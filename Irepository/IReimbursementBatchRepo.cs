using System;
using System.Collections.Generic;

namespace sma.plan
{
	internal interface IReimbursementBatchRepo
	{
		List<ReimbursementBatch> GetAll();

		// The batch, or null if there's none with that id.
		ReimbursementBatch Find(string id);

		ReimbursementBatch Create(ReimbursementBatch batch);

		Tuple<ReimbursementBatch, ReimbursementBatch> Update(ReimbursementBatch batch);
	}
}
