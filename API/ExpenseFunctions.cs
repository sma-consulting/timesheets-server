using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace sma.plan
{
	// Expenses: everyone submits their own and sees their own; admins see all.
	//
	// A claim arrives as one multipart form - the fields plus the receipt file -
	// so an expense can never exist without its receipt. The rules the form
	// shows are repeated here, since the form can't be trusted to enforce them.
	internal class ExpenseFunctions : RequestHandler
	{
		private readonly IExpenseRepo _expenseRepo;
		private readonly IExpenseReceiptRepo _receiptRepo;
		private readonly IProjectRepo _projectRepo;
		private readonly IProjectTaskRepo _taskRepo;
		private readonly IProjectService _projectService;
		private readonly ISecurityService _securityService;

		public ExpenseFunctions(
			IExpenseRepo expenseRepo,
			IExpenseReceiptRepo receiptRepo,
			IProjectRepo projectRepo,
			IProjectTaskRepo taskRepo,
			IProjectService projectService,
			ISecurityService securityService)
		{
			_expenseRepo = expenseRepo;
			_receiptRepo = receiptRepo;
			_projectRepo = projectRepo;
			_taskRepo = taskRepo;
			_projectService = projectService;
			_securityService = securityService;
		}


		// Your own expenses, or everyone's for an admin.
		[FunctionName("GetExpenses")]
		public async Task<IActionResult> RunGetExpenses(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "expense/")] HttpRequest req)
		{
			if (_securityService.IsCurrentUserAdmin())
			{
				return Ok(() => _expenseRepo.GetAll(), (e) => new { expenseList = e });
			}

			string me = _securityService.GetCurrentTeamMemberId();
			if (string.IsNullOrWhiteSpace(me))
			{
				return Forbidden("Sign in to see and submit expenses.");
			}

			return Ok(() => _expenseRepo.GetByTeamMember(me), (e) => new { expenseList = e });
		}


		[FunctionName("CreateExpense")]
		public async Task<IActionResult> RunCreateExpense(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "expense/")] HttpRequest req)
		{
			string me = _securityService.GetCurrentTeamMemberId();
			if (string.IsNullOrWhiteSpace(me))
			{
				return Forbidden("Sign in to see and submit expenses.");
			}

			if (!req.HasFormContentType)
			{
				return Invalid("Send the expense as a form with its receipt attached.");
			}

			IFormCollection form = await req.ReadFormAsync();
			IFormFile file = form.Files["receipt"];

			Expense expense;
			string invalid = Parse(form, out expense);
			if (invalid == null)
			{
				invalid = ValidateReceipt(file);
			}
			if (invalid != null)
			{
				return Invalid(invalid);
			}

			// You can only claim against a project you work on or manage, the
			// same projects the form offers. Admins can claim against any.
			if (!_projectService.MayView(expense.ProjectId))
			{
				return Forbidden("You are not assigned to that project.");
			}

			byte[] bytes;
			using (var buffer = new MemoryStream())
			{
				await file.CopyToAsync(buffer);
				bytes = buffer.ToArray();
			}

			return Ok(
				() =>
				{
					ExpenseReceipt receipt = _receiptRepo.Create(new ExpenseReceipt
					{
						TeamMemberId = me,
						FileName = Path.GetFileName(file.FileName),
						ContentType = file.ContentType,
						Data = bytes,
						UploadedAt = DateTime.UtcNow,
					});

					expense.TeamMemberId = me;
					expense.Status = Expense.StatusSubmitted;
					expense.SubmittedAt = DateTime.UtcNow;
					expense.ReceiptId = receipt.Id;
					expense.ReceiptName = receipt.FileName;
					expense.ReceiptContentType = receipt.ContentType;
					return _expenseRepo.Create(expense);
				},
				(e) => new { expense = e });
		}


		// Owners may delete their own while it is still waiting for review; admins
		// may delete any. The receipt goes with it.
		[FunctionName("DeleteExpense")]
		public async Task<IActionResult> RunDeleteExpense(
			[HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "expense/{id}/delete/")] HttpRequest req, string id)
		{
			Expense expense = _expenseRepo.Get(id);
			if (expense == null)
			{
				return Invalid("That expense no longer exists.");
			}

			bool admin = _securityService.IsCurrentUserAdmin();
			bool mine = expense.TeamMemberId == _securityService.GetCurrentTeamMemberId();

			if (!admin && !mine)
			{
				return Forbidden("That expense belongs to someone else.");
			}
			if (!admin && expense.Status != Expense.StatusSubmitted)
			{
				return Invalid("Only expenses still waiting for review can be deleted.");
			}

			return Ok(
				() =>
				{
					if (!string.IsNullOrWhiteSpace(expense.ReceiptId))
					{
						_receiptRepo.Delete(expense.ReceiptId);
					}
					return _expenseRepo.Delete(id);
				},
				(e) => new { deletedExpense = e });
		}


		// The receipt file itself, for viewing - not the usual JSON envelope,
		// except when it can't be returned.
		[FunctionName("GetExpenseReceipt")]
		public async Task<IActionResult> RunGetExpenseReceipt(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "expense/{id}/receipt/")] HttpRequest req, string id)
		{
			Expense expense = _expenseRepo.Get(id);
			if (expense == null || string.IsNullOrWhiteSpace(expense.ReceiptId))
			{
				return Invalid("That receipt no longer exists.");
			}

			bool admin = _securityService.IsCurrentUserAdmin();
			bool mine = expense.TeamMemberId == _securityService.GetCurrentTeamMemberId();
			if (!admin && !mine)
			{
				return Forbidden("That expense belongs to someone else.");
			}

			ExpenseReceipt receipt = _receiptRepo.Get(expense.ReceiptId);
			if (receipt == null || receipt.Data == null)
			{
				return Invalid("That receipt no longer exists.");
			}

			return new FileContentResult(receipt.Data, receipt.ContentType ?? "application/octet-stream");
		}


		// Reads the form into an Expense and checks it. Returns null when it's
		// valid, otherwise the reason it isn't.
		private string Parse(IFormCollection form, out Expense expense)
		{
			expense = new Expense();

			if (!DateOnly.TryParseExact(form["date"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
			{
				return "A date is required.";
			}
			// A day's grace, so a claim made late in the evening in Canada isn't
			// refused as "in the future" by a server running on UTC.
			if (date > DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1))
			{
				return "The date can't be in the future.";
			}
			expense.Date = date;

			string projectId = form["projectId"];
			if (string.IsNullOrWhiteSpace(projectId) || _projectRepo.Get(projectId) == null)
			{
				return "Select a project.";
			}
			expense.ProjectId = projectId;

			string taskId = form["projectTaskId"];
			if (!string.IsNullOrWhiteSpace(taskId))
			{
				ProjectTask task = _taskRepo.Get(taskId);
				if (task == null || task.ProjectId != projectId)
				{
					return "That subproject doesn't belong to the selected project.";
				}
				expense.ProjectTaskId = taskId;
			}

			string category = form["category"];
			if (!Expense.Categories.Contains(category))
			{
				return "Select a valid type of expense.";
			}
			expense.Category = category;

			expense.Notes = ((string)form["notes"] ?? "").Trim();
			if (Expense.NotesRequired.Contains(category) && expense.Notes.Length == 0)
			{
				return "Notes are required for meals: say who it was with and why.";
			}

			if (!decimal.TryParse(form["amount"], NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount) || amount <= 0)
			{
				return "Enter an amount above 0.";
			}
			if (decimal.Round(amount, 2) != amount)
			{
				return "Use dollars and cents, e.g. 19.99.";
			}
			expense.Amount = amount;

			expense.Billable = string.Equals(form["billable"], "true", StringComparison.OrdinalIgnoreCase);
			return null;
		}

		private static string ValidateReceipt(IFormFile file)
		{
			if (file == null || file.Length == 0)
			{
				return "Attach a receipt or invoice for the expense.";
			}
			if (file.Length > ExpenseReceipt.MaxBytes)
			{
				return "The receipt is too large - it must be under 10 MB.";
			}

			string type = file.ContentType ?? "";
			if (!type.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
				!string.Equals(type, "application/pdf", StringComparison.OrdinalIgnoreCase))
			{
				return "The receipt must be a photo or a PDF.";
			}
			return null;
		}
	}
}
