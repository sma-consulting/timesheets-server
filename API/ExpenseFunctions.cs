using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
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
		private readonly IInvoiceRepo _invoiceRepo;

		public ExpenseFunctions(
			IExpenseRepo expenseRepo,
			IExpenseReceiptRepo receiptRepo,
			IProjectRepo projectRepo,
			IProjectTaskRepo taskRepo,
			IProjectService projectService,
			ISecurityService securityService,
			IInvoiceRepo invoiceRepo)
		{
			_expenseRepo = expenseRepo;
			_receiptRepo = receiptRepo;
			_projectRepo = projectRepo;
			_taskRepo = taskRepo;
			_projectService = projectService;
			_securityService = securityService;
			_invoiceRepo = invoiceRepo;
		}


		// Your own expenses, or everyone's for an admin.
		[Allow(Role.SignedIn)]
		[Function("GetExpenses")]
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
				return Forbidden(Errors.Expenses.SignInRequired);
			}

			return Ok(() => _expenseRepo.GetByTeamMember(me), (e) => new { expenseList = e });
		}


		// For the expense report: every expense for an admin; for a project
		// manager, the expenses on the projects they manage (anyone's, to report
		// on - not to change); for anyone else, none. The Expenses page keeps
		// using GetExpenses, so managers still see only their own there.
		[Allow(Role.SignedIn)]
		[Function("GetReportExpenses")]
		public async Task<IActionResult> RunGetReportExpenses(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reportExpense/")] HttpRequest req)
		{
			if (_securityService.IsCurrentUserAdmin())
			{
				return Ok(() => _expenseRepo.GetAll(), (e) => new { expenseList = e });
			}

			var managed = _projectService.ManagedProjectIds();
			return Ok(
				() => _expenseRepo.GetByProjects(managed),
				(e) => new { expenseList = e });
		}


		[Allow(Role.SignedIn)]
		[Function("CreateExpense")]
		public async Task<IActionResult> RunCreateExpense(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "expense/")] HttpRequest req)
		{
			string me = _securityService.GetCurrentTeamMemberId();
			if (string.IsNullOrWhiteSpace(me))
			{
				return Forbidden(Errors.Expenses.SignInRequired);
			}

			if (!req.HasFormContentType)
			{
				return Invalid(Errors.Expenses.SendAsFormWithReceipt);
			}

			IFormCollection form = await req.ReadFormAsync();
			IFormFile file = form.Files["receipt"];

			Expense expense;
			ApiError invalid = Parse(form, out expense);
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
				return Forbidden(Errors.Expenses.NotOnProject);
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
		[Allow(Role.SignedIn)]
		[Function("UpdateExpense")]
		public async Task<IActionResult> RunUpdateExpense(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "expense/{id}/update/")] HttpRequest req, string id)
		{
			Expense existing = _expenseRepo.Get(id);
			if (existing == null)
			{
				return Invalid(Errors.Expenses.NotFound);
			}

			bool admin = _securityService.IsCurrentUserAdmin();
			bool mine = existing.TeamMemberId == _securityService.GetCurrentTeamMemberId();

			if (!admin && !mine)
			{
				return Forbidden(Errors.Expenses.NotYours);
			}
			// Approval locks an expense for everyone, admins included; an admin
			// sends it back to review to unlock it.
			if (existing.Status == Expense.StatusApproved)
			{
				return Invalid(Errors.Expenses.LockedForChange);
			}

			if (!req.HasFormContentType)
			{
				return Invalid(Errors.Expenses.SendAsForm);
			}

			IFormCollection form = await req.ReadFormAsync();
			IFormFile file = form.Files["receipt"];

			// The receipt is the claimant's evidence, so admins never swap it - they
			// can correct a claim's fields, not the proof behind it.
			if (file != null && admin)
			{
				return Forbidden(Errors.Expenses.AdminCantReplaceReceipt);
			}

			Expense changes;
			ApiError invalid = Parse(form, out changes);
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
				return Forbidden(Errors.Expenses.NotOnProject);
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
					existing.Reimbursable = changes.Reimbursable;

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
		[Allow(Role.Admin)]
		[Function("ReviewExpense")]
		public async Task<IActionResult> RunReviewExpense(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "expense/{id}/review/")] HttpRequest req, string id)
		{
			Expense expense = _expenseRepo.Get(id);
			if (expense == null)
			{
				return Invalid(Errors.Expenses.NotFound);
			}

			string me = _securityService.GetCurrentTeamMemberId();
			if (!string.IsNullOrWhiteSpace(me) && expense.TeamMemberId == me)
			{
				return Forbidden(Errors.Expenses.CantReviewOwn);
			}
			if (expense.Reimbursed)
			{
				return Invalid(Errors.Expenses.AlreadyReimbursed);
			}

			// Approval is what locks an expense, so an expense billed on an issued
			// invoice can't be sent back to review - the invoice must keep matching
			// what it billed. Cancelling the invoice unlocks it, as for time.
			Invoice billedOn = _invoiceRepo.IssuedInvoiceBillingExpense(id);
			if (billedOn != null)
			{
				return Invalid(Errors.Expenses.BilledOnInvoice(billedOn.Number, billedOn.Month));
			}

			ReviewRequest review = JsonConvert.DeserializeObject<ReviewRequest>(
				await new StreamReader(req.Body).ReadToEndAsync());
			string status = review?.Status;
			string reason = (review?.RejectionReason ?? "").Trim();

			if (status != Expense.StatusApproved &&
				status != Expense.StatusRejected &&
				status != Expense.StatusSubmitted)
			{
				return Invalid(Errors.Expenses.ChooseReviewAction);
			}
			if (status == Expense.StatusRejected && reason.Length == 0)
			{
				return Invalid(Errors.Expenses.RejectionReasonRequired);
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
		[Allow(Role.SignedIn)]
		[Function("DeleteExpense")]
		public async Task<IActionResult> RunDeleteExpense(
			[HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "expense/{id}/")] HttpRequest req, string id)
		{
			Expense expense = _expenseRepo.Get(id);
			if (expense == null)
			{
				return Invalid(Errors.Expenses.NotFound);
			}

			bool admin = _securityService.IsCurrentUserAdmin();
			bool mine = expense.TeamMemberId == _securityService.GetCurrentTeamMemberId();

			if (!admin && !mine)
			{
				return Forbidden(Errors.Expenses.NotYours);
			}
			if (expense.Status == Expense.StatusApproved)
			{
				return Invalid(Errors.Expenses.LockedForDelete);
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
		[Allow(Role.SignedIn)]
		[Function("GetExpenseReceipt")]
		public async Task<IActionResult> RunGetExpenseReceipt(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "expense/{id}/receipt/")] HttpRequest req, string id)
		{
			Expense expense = _expenseRepo.Get(id);
			if (expense == null || string.IsNullOrWhiteSpace(expense.ReceiptId))
			{
				return Invalid(Errors.Expenses.ReceiptNotFound);
			}

			bool admin = _securityService.IsCurrentUserAdmin();
			bool mine = expense.TeamMemberId == _securityService.GetCurrentTeamMemberId();
			// A project manager opens the receipts on their projects' invoices.
			bool manager = !admin && !mine && _projectService.ManagedProjectIds().Contains(expense.ProjectId);
			if (!admin && !mine && !manager)
			{
				return Forbidden(Errors.Expenses.NotYours);
			}

			ExpenseReceipt receipt = _receiptRepo.Find(expense.ReceiptId);
			Stream file = receipt == null ? null : _receiptRepo.OpenRead(receipt);
			if (file == null)
			{
				return Invalid(Errors.Expenses.ReceiptNotFound);
			}

			// Streamed from storage rather than loaded whole; disposed once sent.
			return new FileStreamResult(file, receipt.ContentType ?? "application/octet-stream");
		}


		// Reads the form into an Expense and checks it. Returns null when it's
		// valid, otherwise the reason it isn't.
		private ApiError Parse(IFormCollection form, out Expense expense)
		{
			expense = new Expense();

			if (!DateOnly.TryParseExact(form["date"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
			{
				return Errors.Expenses.DateRequired;
			}
			// A day's grace, so a claim made late in the evening in Canada isn't
			// refused as "in the future" by a server running on UTC.
			if (date > DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1))
			{
				return Errors.Expenses.DateInFuture;
			}
			expense.Date = date;

			string projectId = form["projectId"];
			if (string.IsNullOrWhiteSpace(projectId) || _projectRepo.Get(projectId) == null)
			{
				return Errors.Expenses.ProjectRequired;
			}
			expense.ProjectId = projectId;

			string taskId = form["projectTaskId"];
			if (!string.IsNullOrWhiteSpace(taskId))
			{
				ProjectTask task = _taskRepo.Get(taskId);
				if (task == null || task.ProjectId != projectId)
				{
					return Errors.Expenses.SubprojectNotInProject;
				}
				expense.ProjectTaskId = taskId;
			}

			string category = form["category"];
			if (!Expense.Categories.Contains(category))
			{
				return Errors.Expenses.CategoryInvalid;
			}
			expense.Category = category;

			expense.Notes = ((string)form["notes"] ?? "").Trim();
			if (Expense.NotesRequired.Contains(category) && expense.Notes.Length == 0)
			{
				return Errors.Expenses.MealNotesRequired;
			}

			if (!decimal.TryParse(form["amount"], NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount) || amount <= 0)
			{
				return Errors.Expenses.AmountRequired;
			}
			if (decimal.Round(amount, 2) != amount)
			{
				return Errors.Expenses.AmountCents;
			}
			expense.Amount = amount;

			expense.Billable = string.Equals(form["billable"], "true", StringComparison.OrdinalIgnoreCase);
			// Paid back unless it says otherwise: most claims are someone's own
			// money.
			expense.Reimbursable = !string.Equals(form["reimbursable"], "false", StringComparison.OrdinalIgnoreCase);
			return null;
		}

		private static ApiError ValidateReceipt(IFormFile file)
		{
			if (file == null || file.Length == 0)
			{
				return Errors.Expenses.ReceiptRequired;
			}
			if (file.Length > ExpenseReceipt.MaxBytes)
			{
				return Errors.Expenses.ReceiptTooLarge;
			}

			string type = file.ContentType ?? "";
			if (!type.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
				!string.Equals(type, "application/pdf", StringComparison.OrdinalIgnoreCase))
			{
				return Errors.Expenses.ReceiptType;
			}
			return null;
		}
	}
}
