using System;
using System.Collections.Generic;

namespace sma.plan
{
	internal interface IExpenseReceiptRepo
	{
		ExpenseReceipt Create(ExpenseReceipt data);
		ExpenseReceipt Delete(string id);
		ExpenseReceipt Get(string id);
	}
}
