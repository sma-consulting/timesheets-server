using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace sma.plan
{
	// Projects' financials: the budget's parts as in Access's qryBudget, and the
	// disbursement rate invoices are built with. Admins set them; a project's
	// manager may look at their own projects'. The page sends the parts; the
	// server checks them and works out every total, so what's stored always
	// adds up.
	internal class ProjectFinancialsFunctions : RequestHandler
	{
		private readonly IProjectFinancialsRepo _financialsRepo;
		private readonly IProjectRepo _projectRepo;
		private readonly IExpenseRepo _expenseRepo;
		private readonly IProjectService _projectService;
		private readonly ISecurityService _securityService;

		public ProjectFinancialsFunctions(
			IProjectFinancialsRepo financialsRepo,
			IProjectRepo projectRepo,
			IExpenseRepo expenseRepo,
			IProjectService projectService,
			ISecurityService securityService)
		{
			_financialsRepo = financialsRepo;
			_projectRepo = projectRepo;
			_expenseRepo = expenseRepo;
			_projectService = projectService;
			_securityService = securityService;
		}

		// Every project's financials that has any - all of them for an admin,
		// just their own projects' for a project manager, none for anyone else.
		// A project without is simply left out: it has no budget yet.
		//
		// With them, each project's approved billable expenses not yet invoiced:
		// part of what it has to bill, which a manager can't add up for
		// themselves, as other people's expenses aren't theirs to read.
		[Allow(Role.SignedIn)]
		[Function("GetAllProjectFinancials")]
		public async Task<IActionResult> RunGetAllProjectFinancials(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projectFinancials/")] HttpRequest req)
		{
			bool admin = _securityService.IsCurrentUserAdmin();
			HashSet<string> managed = admin ? null : _projectService.ManagedProjectIds();

			return Ok(
				() =>
				{
					if (admin)
					{
						// Financials still on project documents from before they had
						// their own are copied across first.
						_financialsRepo.EnsureAllFromLegacy();
					}
					return new
					{
						financialsList = admin ? _financialsRepo.GetAll() : _financialsRepo.GetByProjects(managed),
						uninvoicedExpenseList = _expenseRepo.GetUninvoicedBillable(admin ? null : managed)
							.GroupBy(e => e.ProjectId)
							.Select(g => new
							{
								projectId = g.Key,
								amount = g.Sum(e => e.Amount),
								count = g.Count(),
							})
							.ToList(),
					};
				},
				(r) => r);
		}

		// Creates or replaces a project's financials.
		[Allow(Role.Admin)]
		[Function("SaveProjectFinancials")]
		public async Task<IActionResult> RunSaveProjectFinancials(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projectFinancials/{projectId}/")] HttpRequest req,
			string projectId)
		{
			ProjectFinancials financials = JsonConvert.DeserializeObject<ProjectFinancials>(
				await new StreamReader(req.Body).ReadToEndAsync());
			if (financials == null)
			{
				return Invalid("The financials are missing.");
			}
			if (!_projectRepo.Exists(projectId))
			{
				return Invalid("That project no longer exists.");
			}

			financials.ProjectId = projectId;
			string invalid = Validate(financials);
			if (invalid != null)
			{
				return Invalid(invalid);
			}
			financials.UpdatedAt = DateTime.UtcNow;
			financials.UpdatedBy = _securityService.GetCurrentUserId();

			return Ok(
				() => _financialsRepo.Save(financials),
				(f) => new
				{
					financials = f
				});
		}

		// Returns null when the financials are valid, otherwise the reason they
		// aren't. Also works out every total from what they're made of.
		private static string Validate(ProjectFinancials financials)
		{
			if (financials.DisbursementRate < 0 || financials.DisbursementRate > 100)
			{
				return "The disbursement rate must be between 0 and 100%.";
			}
			if (financials.ContractValue < 0)
			{
				return "The contract value can't be negative.";
			}

			// The itemised parts: every item a type of expense and an amount, and
			// together the part's total. Only a change order can be negative.
			financials.ChangeOrders ??= new List<BudgetItem>();
			financials.MajorDisbursements ??= new List<BudgetItem>();
			financials.ProjectDisbursements ??= new List<BudgetItem>();
			string itemsInvalid =
				CheckItems(financials.ChangeOrders, "change order", true) ??
				CheckItems(financials.MajorDisbursements, "major disbursement", false) ??
				CheckItems(financials.ProjectDisbursements, "project disbursement", false);
			if (itemsInvalid != null)
			{
				return itemsInvalid;
			}
			if (financials.ChangeOrders.Count > 0)
			{
				financials.ChangeOrder = financials.ChangeOrders.Sum(i => i.Amount);
			}
			if (financials.MajorDisbursements.Count > 0)
			{
				financials.MajorDisbursement = financials.MajorDisbursements.Sum(i => i.Amount);
			}
			if (financials.ProjectDisbursements.Count > 0)
			{
				financials.ProjectDisbursement = financials.ProjectDisbursements.Sum(i => i.Amount);
			}

			// Subconsultants: each a contractor's name, what SMA pays them and
			// its markup; what the client is billed is worked out from those.
			financials.Subconsultants ??= new List<SubconsultantItem>();
			foreach (SubconsultantItem item in financials.Subconsultants)
			{
				if (item == null || string.IsNullOrWhiteSpace(item.Contractor))
				{
					return "Enter the contractor for each subconsultant.";
				}
				if (item.Cost < 0 || item.Markup < 0)
				{
					return "A subconsultant's cost and markup can't be negative.";
				}
				item.Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id;
				item.Contractor = item.Contractor.Trim();
				item.Description = item.Description?.Trim();
				item.Amount = Math.Round(item.Cost * (1 + item.Markup / 100), 2, MidpointRounding.AwayFromZero);
			}
			if (financials.Subconsultants.Count > 0)
			{
				financials.Subconsultant = financials.Subconsultants.Sum(i => i.Amount);
			}

			if (financials.MajorDisbursement < 0 || financials.ProjectDisbursement < 0 ||
				financials.Subconsultant < 0)
			{
				return "The disbursements and subconsultant amounts can't be negative.";
			}

			// The total budget is the sum of its parts. Until one is set, a
			// budget from before the parts keeps the figure it had.
			bool hasParts = financials.ContractValue != 0 || financials.ChangeOrder != 0 ||
				financials.MajorDisbursement != 0 || financials.ProjectDisbursement != 0 ||
				financials.Subconsultant != 0;
			if (hasParts)
			{
				financials.TotalBudget = financials.ContractValue + financials.ChangeOrder +
					financials.MajorDisbursement + financials.ProjectDisbursement + financials.Subconsultant;
			}
			return null;
		}

		// Checks one itemised part - each item needs a type of expense, and an
		// amount that isn't negative unless `mayBeNegative` - and tidies its
		// items: an id for new ones, descriptions trimmed. Returns null when
		// they're all fine, otherwise the reason.
		private static string CheckItems(List<BudgetItem> items, string name, bool mayBeNegative)
		{
			foreach (BudgetItem item in items)
			{
				if (item == null || !Expense.Categories.Contains(item.Category))
				{
					return $"Choose a type of expense for each {name}.";
				}
				if (!mayBeNegative && item.Amount < 0)
				{
					return $"A {name} can't be negative.";
				}
				item.Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id;
				item.Description = item.Description?.Trim();
			}
			return null;
		}
	}
}
