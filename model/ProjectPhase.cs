using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class ProjectPhase
	{
        public int PhaseNumber {  get; set; }

		public string FundingCodeOverride { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string CalendarId { get; set; }

		public DateOnly? StartDate { get; set; }

		public DateOnly? EndDate { get; set; }

        public int Duration { get; set; }

		public decimal BudgetAmount { get; set; }

		public int BudgetConfidenceLevel { get; set; }

		[BsonRepresentation(BsonType.ObjectId)]
		public string BudgetSpendCurveId { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string ScheduleConfidenceDistributionId { get; set; }

        public List<ProjectRoleRequirement> RoleRequirements { get; set; } = new List<ProjectRoleRequirement>();

		public bool IsSCBA { get; set; } = false;

		public DateOnly? SpecialMilestoneDate { get; set; }

	}
}
