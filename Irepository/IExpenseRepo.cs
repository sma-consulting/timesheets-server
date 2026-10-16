using System;
using System.Collections.Generic;

namespace sma.plan
{
	internal interface IExpenseRepo
	{
		Expense Create(Expense data);
		Expense Delete(string id);
		Expense Get(string id);
		List<Expense> GetAll();
		Tuple<Expense, Expense> Update(Expense data);
		List<Expense> GetByTeamMember(string teamMemberId);
	}
}
