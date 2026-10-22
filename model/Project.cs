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

		// Where the project stands, as in SMA's Access database (tblJob.Status).
		public const string StatusInProcess = "In process";
		public const string StatusFinished = "Finished";
		public const string StatusCancelled = "Cancelled";
		public const string StatusSma = "SMA";

		public static readonly string[] Statuses = { StatusInProcess, StatusFinished, StatusCancelled, StatusSma };

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

		// One of Statuses; null on projects saved before there was a status.
		public string Status { get; set; }

		// The project's budget, disbursement rate and the rest of its financials
		// are in ProjectFinancials: admins only, and saved on their own.

		// Derived from ProjectType on every save (false only for NonBillable), so
		// the two can't disagree.
		public bool Billable { get; set; } = true;

		// "Z Paid Hours Off": the project everyone logs vacation, leave, stat
		// holidays and lunch breaks to (see PaidTimeOffSeed). Anyone signed in may
		// log time to it without being assigned, and it can't be deleted. Set
		// only by the seed - ProjectService keeps it as stored, whatever a save
		// sends.
		public bool PaidTimeOff { get; set; }

		public List<ProjectPhase> Phases { get; set; } = new List<ProjectPhase>();
	}
}
