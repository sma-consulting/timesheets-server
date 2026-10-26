using System;
using System.Collections.Generic;

namespace sma.plan
{
	internal interface IReimbursementBatchRepo
	{
		List<ReimbursementBatch> GetAll();

		// The batch, or null if there's none with that id.
		ReimbursementBatch Find(string id);

		// Every batch, its people without their expenses - most of a batch's
		// size, and only needed to print it.
		List<ReimbursementBatch> GetWithoutExpenses();

		ReimbursementBatch Create(ReimbursementBatch batch);

		Tuple<ReimbursementBatch, ReimbursementBatch> Update(ReimbursementBatch batch);
	}
}
