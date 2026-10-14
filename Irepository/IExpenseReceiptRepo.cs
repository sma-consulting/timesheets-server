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
		// The receipt's file, for sending back to the browser; null if the file
		// is missing from storage.
		Stream OpenRead(ExpenseReceipt receipt);
	}
}
