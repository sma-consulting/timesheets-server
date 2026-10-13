using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace sma.plan
{
	// A project's financials, as in Access's qryBudget: its contract value,
	// change orders, disbursements and subconsultants, and the total budget
	// they make. One per project, kept apart from Project because it's for
	// admins only - every project's team gets the Project document - and so
	// that saving the project's details can't overwrite it, or the other way
	// round.
	//
	// Change orders, major disbursements, project disbursements and
	// subconsultants are itemised; each part's total is worked out from its
	// items on every save. A project from before the items keeps the figure it
	// had until one is added.
	internal class ProjectFinancials : GeneralModel
	{
		[BsonRepresentation(BsonType.ObjectId)]
		public string ProjectId { get; set; }

		// The initial contract value.
		public decimal ContractValue { get; set; }

		// Change orders since; negative for a reduction.
		public List<BudgetItem> ChangeOrders { get; set; } = new List<BudgetItem>();

		public decimal ChangeOrder { get; set; }

		// The contract's major disbursement expenses.
		public List<BudgetItem> MajorDisbursements { get; set; } = new List<BudgetItem>();

		public decimal MajorDisbursement { get; set; }

		// The project's disbursements.
		public List<BudgetItem> ProjectDisbursements { get; set; } = new List<BudgetItem>();

		public decimal ProjectDisbursement { get; set; }

		// The outside contractors SMA hires for part of the work. Their total is
		// what the client is billed for them, cost plus markup.
		public List<SubconsultantItem> Subconsultants { get; set; } = new List<SubconsultantItem>();

		public decimal Subconsultant { get; set; }

		// The flat disbursement's percentage of an invoice's fees (3 for 3%).
		// Null means the standard rate; 0 means no disbursement.
		public decimal? DisbursementRate { get; set; }

		// ContractValue + ChangeOrder + MajorDisbursement + ProjectDisbursement +
		// Subconsultant, worked out on every save. A project whose budget was
		// one figure, from before the parts, keeps it until a part is set.
		public decimal TotalBudget { get; set; }

		public DateTime? UpdatedAt { get; set; }

		// The User who last changed it.
		[BsonRepresentation(BsonType.ObjectId)]
		public string UpdatedBy { get; set; }

		// From the financials a project document carried before they had a
		// document of their own.
		public static ProjectFinancials FromLegacy(LegacyProjectFinancials legacy)
		{
			return new ProjectFinancials
			{
				ProjectId = legacy.Id,
				ContractValue = legacy.ContractValue,
				ChangeOrders = legacy.ChangeOrders ?? new List<BudgetItem>(),
				ChangeOrder = legacy.ChangeOrder,
				MajorDisbursements = legacy.MajorDisbursements ?? new List<BudgetItem>(),
				MajorDisbursement = legacy.MajorDisbursement,
				ProjectDisbursements = legacy.ProjectDisbursements ?? new List<BudgetItem>(),
				ProjectDisbursement = legacy.ProjectDisbursement,
				Subconsultants = legacy.Subconsultants ?? new List<SubconsultantItem>(),
				Subconsultant = legacy.Subconsultant,
				DisbursementRate = legacy.DisbursementRate,
				TotalBudget = legacy.TotalBudget,
				UpdatedAt = DateTime.UtcNow,
			};
		}
	}

	// One item of a project's budget, such as a major disbursement: its type
	// of expense (one of Expense.Categories), what it's for, and how much.
	internal class BudgetItem
	{
		public string Id { get; set; }

		public string Category { get; set; }

		public string Description { get; set; }

		public decimal Amount { get; set; }
	}

	// A subconsultant on a project: the contractor (a person or firm, by name -
	// they don't use the app), what they're doing, what SMA pays them, and the
	// markup SMA adds when billing the client for their work.
	internal class SubconsultantItem
	{
		public string Id { get; set; }

		public string Contractor { get; set; }

		public string Description { get; set; }

		// What SMA pays the contractor.
		public decimal Cost { get; set; }

		// SMA's markup on that, as a percentage (15 for 15%).
		public decimal Markup { get; set; }

		// What the client is billed: Cost plus Markup, worked out on every save.
		// This is what counts towards the budget.
		public decimal Amount { get; set; }
	}

	// The financial fields a Project document carried before they moved to
	// ProjectFinancials - read straight from the Project collection, only to
	// copy them across. Never written.
	internal class LegacyProjectFinancials : GeneralModel
	{
		public decimal ContractValue { get; set; }

		public List<BudgetItem> ChangeOrders { get; set; }

		public decimal ChangeOrder { get; set; }

		public List<BudgetItem> MajorDisbursements { get; set; }

		public decimal MajorDisbursement { get; set; }

		public List<BudgetItem> ProjectDisbursements { get; set; }

		public decimal ProjectDisbursement { get; set; }

		public List<SubconsultantItem> Subconsultants { get; set; }

		public decimal Subconsultant { get; set; }

		public decimal? DisbursementRate { get; set; }

		public decimal TotalBudget { get; set; }

		// Whether there's anything to copy.
		public bool HasAny()
		{
			return ContractValue != 0 || ChangeOrder != 0 || MajorDisbursement != 0 ||
				ProjectDisbursement != 0 || Subconsultant != 0 || TotalBudget != 0 ||
				DisbursementRate.HasValue ||
				(ChangeOrders?.Any() ?? false) || (MajorDisbursements?.Any() ?? false) ||
				(ProjectDisbursements?.Any() ?? false) || (Subconsultants?.Any() ?? false);
		}
	}
}
