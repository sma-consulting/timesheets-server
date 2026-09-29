using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Newtonsoft.Json;
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


		// Changes an expense. Same form as submitting, but the receipt is optional:
		// leave it out to keep the current one, attach one to replace it.
		//
		// Approved expenses are locked for everyone. Otherwise owners may change
		// their own, which sends it back to Submitted for a fresh review - a fixed
		// claim needs looking at again - and admins may change any, leaving the
		// status alone, but may not replace its receipt.
		[FunctionName("UpdateExpense")]
		public async Task<IActionResult> RunUpdateExpense(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "expense/{id}/update/")] HttpRequest req, string id)
		{
			Expense existing = _expenseRepo.Get(id);
			if (existing == null)
			{
				return Invalid("That expense no longer exists.");
			}

			bool admin = _securityService.IsCurrentUserAdmin();
			bool mine = existing.TeamMemberId == _securityService.GetCurrentTeamMemberId();

			if (!admin && !mine)
			{
				return Forbidden("That expense belongs to someone else.");
			}
			// Approval locks an expense for everyone, admins included; an admin
			// sends it back to review to unlock it.
			if (existing.Status == Expense.StatusApproved)
			{
				return Invalid("This expense is approved and locked. Send it back to review to change it.");
			}

			if (!req.HasFormContentType)
			{
				return Invalid("Send the expense as a form.");
			}

			IFormCollection form = await req.ReadFormAsync();
			IFormFile file = form.Files["receipt"];

			// The receipt is the claimant's evidence, so admins never swap it - they
			// can correct a claim's fields, not the proof behind it.
			if (file != null && admin)
			{
				return Forbidden("Admins can't replace receipts.");
			}

			Expense changes;
			string invalid = Parse(form, out changes);
			if (invalid == null && file != null)
			{
				invalid = ValidateReceipt(file);
			}
			if (invalid != null)
			{
				return Invalid(invalid);
			}

			if (!_projectService.MayView(changes.ProjectId))
			{
				return Forbidden("You are not assigned to that project.");
			}

			byte[] bytes = null;
			if (file != null)
			{
				using (var buffer = new MemoryStream())
				{
					await file.CopyToAsync(buffer);
					bytes = buffer.ToArray();
				}
			}

			return Ok(
				() =>
				{
					existing.ProjectId = changes.ProjectId;
					existing.ProjectTaskId = changes.ProjectTaskId;
					existing.Date = changes.Date;
					existing.Category = changes.Category;
					existing.Notes = changes.Notes;
					existing.Amount = changes.Amount;
					existing.Billable = changes.Billable;

					if (!admin)
					{
						existing.Status = Expense.StatusSubmitted;
						existing.ReviewedBy = null;
						existing.ReviewedAt = null;
						existing.RejectionReason = null;
					}

					if (bytes != null)
					{
						string oldReceiptId = existing.ReceiptId;
						ExpenseReceipt receipt = _receiptRepo.Create(new ExpenseReceipt
						{
							TeamMemberId = existing.TeamMemberId,
							FileName = Path.GetFileName(file.FileName),
							ContentType = file.ContentType,
							Data = bytes,
							UploadedAt = DateTime.UtcNow,
						});
						existing.ReceiptId = receipt.Id;
						existing.ReceiptName = receipt.FileName;
						existing.ReceiptContentType = receipt.ContentType;

						if (!string.IsNullOrWhiteSpace(oldReceiptId))
						{
							_receiptRepo.Delete(oldReceiptId);
						}
					}

					return _expenseRepo.Update(existing).Item2;
				},
				(e) => new { expense = e });
		}


		// An admin's decision on a claim: Approved, Rejected (with a reason), or
		// back to Submitted to undo a decision - which is also how an approved,
		// locked claim is unlocked. Admins can't review their own claims, and a
		// reimbursed claim is settled and can't be reviewed again.
		[FunctionName("ReviewExpense")]
		public async Task<IActionResult> RunReviewExpense(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "expense/{id}/review/")] HttpRequest req, string id)
		{
			if (!_securityService.IsCurrentUserAdmin())
			{
				return Forbidden("Only admins can review expenses.");
			}

			Expense expense = _expenseRepo.Get(id);
			if (expense == null)
			{
				return Invalid("That expense no longer exists.");
			}

			string me = _securityService.GetCurrentTeamMemberId();
			if (!string.IsNullOrWhiteSpace(me) && expense.TeamMemberId == me)
			{
				return Forbidden("Another admin must review your own expenses.");
			}
			if (expense.Reimbursed)
			{
				return Invalid("This expense has been reimbursed and can't be reviewed again.");
			}

			ReviewRequest review = JsonConvert.DeserializeObject<ReviewRequest>(
				await new StreamReader(req.Body).ReadToEndAsync());
			string status = review?.Status;
			string reason = (review?.RejectionReason ?? "").Trim();

			if (status != Expense.StatusApproved &&
				status != Expense.StatusRejected &&
				status != Expense.StatusSubmitted)
			{
				return Invalid("Choose approve, reject or back to review.");
			}
			if (status == Expense.StatusRejected && reason.Length == 0)
			{
				return Invalid("Give a reason for rejecting the expense.");
			}

			return Ok(
				() =>
				{
					expense.Status = status;
					if (status == Expense.StatusSubmitted)
					{
						expense.ReviewedBy = null;
						expense.ReviewedAt = null;
						expense.RejectionReason = null;
					}
					else
					{
						expense.ReviewedBy = me;
						expense.ReviewedAt = DateTime.UtcNow;
						expense.RejectionReason = status == Expense.StatusRejected ? reason : null;
					}
					return _expenseRepo.Update(expense).Item2;
				},
				(e) => new { expense = e });
		}

		private class ReviewRequest
		{
			public string Status { get; set; }

			public string RejectionReason { get; set; }
		}


		// Owners may delete their own, and admins any, until it's approved -
		// approval locks it. The receipt goes with it.
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
			if (expense.Status == Expense.StatusApproved)
			{
				return Invalid("This expense is approved and locked. Send it back to review to delete it.");
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
