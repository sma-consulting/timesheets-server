using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class Project : GeneralModel
	{

		// How the customer is charged for the project.
		public const string TypeHourly = "Hourly";
		public const string TypeLumpSum = "LumpSum";
		public const string TypeNonBillable = "NonBillable";

		public static readonly string[] ProjectTypes = { TypeHourly, TypeLumpSum, TypeNonBillable };

		[BsonRepresentation(BsonType.ObjectId)]
		public string CustomerId { get; set; }

		public string Name { get; set; }

		// No longer edited in the UI; kept so values already stored survive a save.
		public string ShortName { get; set; }

		// Shown in the UI as the SMA Project Code.
        public string FundingCode { get; set; }

		public string Division { get; set; }

		// The customer's purchase order, quoted on invoices.
		public string PoNumber { get; set; }

		public DateOnly? StartDate { get; set; }

		public DateOnly? EndDate { get; set; }

		public string Notes { get; set; }

		public string ProjectType { get; set; } = TypeHourly;

		public bool HighPriority { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
		public string ProjectManagerId { get; set; }

		public decimal TotalBudget { get; set; }

		// The flat disbursement's percentage of an invoice's fees (3 for 3%).
		// Null means the standard rate; 0 means no disbursement.
		public decimal? DisbursementRate { get; set; }

		// Derived from ProjectType on every save (false only for NonBillable), so
		// the two can't disagree.
		public bool Billable { get; set; } = true;

		public List<ProjectPhase> Phases { get; set; } = new List<ProjectPhase>();
	}
}
