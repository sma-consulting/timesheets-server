using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace sma.plan
{
	// Reimbursements, for admins. On the 15th, every approved, reimbursable
	// expense not yet paid back and dated on or before the batch date is paid
	// back, grouped by person. The page previews the batch; filing it here is
	// what saves it and marks its expenses reimbursed, so no later batch pays
	// them again. The server works out what's in it itself - the page only
	// says which date, and what it showed, so nothing it didn't show is paid.
	internal class ReimbursementFunctions : RequestHandler
	{
		private readonly IReimbursementBatchRepo _batchRepo;
		private readonly IExpenseRepo _expenseRepo;
		private readonly ITeamMemberRepo _teamMemberRepo;
		private readonly ISecurityService _securityService;

		public ReimbursementFunctions(
			IReimbursementBatchRepo batchRepo,
			IExpenseRepo expenseRepo,
			ITeamMemberRepo teamMemberRepo,
			ISecurityService securityService)
		{
			_batchRepo = batchRepo;
			_expenseRepo = expenseRepo;
			_teamMemberRepo = teamMemberRepo;
			_securityService = securityService;
		}

		// Every filed batch, newest first, voided ones included.
		[Allow(Role.Admin)]
		[Function("GetReimbursementBatches")]
		public async Task<IActionResult> RunGetReimbursementBatches(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reimbursement/")] HttpRequest req)
		{
			return Ok(
				() => _batchRepo.GetAll().OrderByDescending(b => b.FiledAt).ToList(),
				(b) => new
				{
					batchList = b
				});
		}


		// The expenses still to be paid back - approved, reimbursable, not yet
		// reimbursed, whatever their date - for the page's preview of a batch.
		// The same ones filing checks against.
		[Allow(Role.Admin)]
		[Function("GetAwaitingReimbursement")]
		public async Task<IActionResult> RunGetAwaitingReimbursement(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reimbursement/awaiting/")] HttpRequest req)
		{
			return Ok(
				() => _expenseRepo.GetAwaitingReimbursement(),
				(e) => new
				{
					expenseList = e
				});
		}


		// Files the batch for a date. `expenseIds` are the expenses the page
		// showed: if the batch has changed since (a claim approved or sent back
		// meanwhile), nothing is filed and the admin checks it again.
		[Allow(Role.Admin)]
		[Function("FileReimbursementBatch")]
		public async Task<IActionResult> RunFileReimbursementBatch(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "reimbursement/")] HttpRequest req)
		{
			FileRequest request = JsonConvert.DeserializeObject<FileRequest>(
				await new StreamReader(req.Body).ReadToEndAsync());

			if (!DateOnly.TryParseExact(request?.BatchDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly batchDate))
			{
				return Invalid("Choose the batch date.");
			}
			// A batch can be filed before its 15th, while its period is still
			// running - anything approved for it afterwards goes in the next
			// batch. But not for a period that hasn't started yet: its 15th can
			// be at most a month away (with a day's grace, as the server runs on
			// UTC).
			if (batchDate > DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(1).AddDays(1))
			{
				return Invalid("That period hasn't started yet.");
			}

			// The date is checked here rather than in the query: dates are stored
			// as {Year, Month, Day} documents, which the database isn't relied on
			// to compare.
			List<Expense> due = _expenseRepo.GetAwaitingReimbursement()
				.Where(e => e.Date <= batchDate)
				.ToList();
			if (due.Count == 0)
			{
				return Invalid("Nothing is waiting to be reimbursed up to that date.");
			}
			if (request.ExpenseIds == null || !due.Select(e => e.Id).ToHashSet().SetEquals(request.ExpenseIds))
			{
				return Invalid("The batch has changed since it was shown. Check it again before filing.");
			}

			Dictionary<string, string> names = _teamMemberRepo.GetByIds(due.Select(e => e.TeamMemberId))
				.Where(m => !string.IsNullOrWhiteSpace(m.Id))
				.GroupBy(m => m.Id)
				.ToDictionary(g => g.Key, g => g.First().Name);

			List<ReimbursementPerson> people = due
				.GroupBy(e => e.TeamMemberId ?? "")
				.Select(g => new ReimbursementPerson
				{
					TeamMemberId = string.IsNullOrEmpty(g.Key) ? null : g.Key,
					Name = names.TryGetValue(g.Key, out string name) ? name : null,
					Expenses = g.OrderBy(e => e.Date).ToList(),
					Subtotal = g.Sum(e => e.Amount),
				})
				.OrderBy(p => p.Name ?? "")
				.ToList();

			return Ok(
				() =>
				{
					ReimbursementBatch batch = _batchRepo.Create(new ReimbursementBatch
					{
						BatchDate = batchDate,
						FiledAt = DateTime.UtcNow,
						FiledBy = _securityService.GetCurrentUserId(),
						People = people,
						Total = people.Sum(p => p.Subtotal),
						Count = due.Count,
					});

					foreach (Expense expense in due)
					{
						expense.Reimbursed = true;
						expense.ReimbursementBatchId = batch.Id;
						expense.ReimbursedAt = batch.FiledAt;
						_expenseRepo.Update(expense);
					}
					return batch;
				},
				(b) => new
				{
					batch = b
				});
		}


		// Undoes a batch filed by mistake. It stays on record, marked voided;
		// its expenses are no longer reimbursed and go in the next batch.
		[Allow(Role.Admin)]
		[Function("VoidReimbursementBatch")]
		public async Task<IActionResult> RunVoidReimbursementBatch(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "reimbursement/{id}/void/")] HttpRequest req, string id)
		{
			ReimbursementBatch batch = _batchRepo.Find(id);
			if (batch == null)
			{
				return Invalid("That batch no longer exists.");
			}
			if (batch.Voided)
			{
				return Invalid("That batch is already voided.");
			}

			return Ok(
				() =>
				{
					foreach (Expense expense in _expenseRepo.GetByReimbursementBatch(id))
					{
						expense.Reimbursed = false;
						expense.ReimbursementBatchId = null;
						expense.ReimbursedAt = null;
						_expenseRepo.Update(expense);
					}

					batch.Voided = true;
					batch.VoidedAt = DateTime.UtcNow;
					batch.VoidedBy = _securityService.GetCurrentUserId();
					return _batchRepo.Update(batch).Item2;
				},
				(b) => new
				{
					batch = b
				});
		}


		private class FileRequest
		{
			public string BatchDate { get; set; }

			public List<string> ExpenseIds { get; set; }
		}
	}
}
