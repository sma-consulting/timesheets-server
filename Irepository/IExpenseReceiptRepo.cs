using System;
using System.Collections.Generic;
using System.IO;

namespace sma.plan
{
	internal interface IExpenseReceiptRepo
	{
		// Uploads data.Data to blob storage, then saves the receipt without it.
		ExpenseReceipt Create(ExpenseReceipt data);
		// Removes the receipt and its file.
		ExpenseReceipt Delete(string id);
		ExpenseReceipt Get(string id);
		// The receipt, or null if there's none with that id.
		ExpenseReceipt Find(string id);
		// A read-only link straight to the receipt's file in blob storage, for
		// `lifetime`; null if it has no file there.
		Uri ReadLink(ExpenseReceipt receipt, TimeSpan lifetime);
	}
}
