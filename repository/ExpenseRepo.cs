using MongoDB.Driver;
using System.Collections.Generic;

namespace sma.plan
{
	internal class ExpenseRepo : DatabaseRepo<Expense>, IExpenseRepo
	{
		public List<Expense> GetByTeamMember(string teamMemberId)
		{
			var filter = Builders<Expense>.Filter.Eq(e => e.TeamMemberId, teamMemberId);
			return DBCollection.Find(filter).ToList();
		}
	}
}
