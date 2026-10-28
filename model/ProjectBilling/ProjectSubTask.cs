using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class ProjectSubTask: GeneralModel
	{
		[BsonRepresentation(BsonType.ObjectId)]
		public string CustomerId { get; set; }

		[BsonRepresentation(BsonType.ObjectId)]
		public string ProjectId { get; set; }

		[BsonRepresentation(BsonType.ObjectId)]
		public string ProjectTaskId { get; set; }

		public string Name { get; set; }

		public string ShortName { get; set; }

		// No FundingCode: only the project has a code (Project.FundingCode).
		// Sub-tasks saved with one still load - GeneralModel ignores extra elements.

		public bool Billable { get; set; } = true;

		// On the paid hours off project, which kind of time this task is - one of
		// TimeOffTypes - so the Staff hours checks can tell a stat holiday from
		// vacation whatever the task is called. Null for ordinary work. Set only
		// by PaidTimeOffSeed; ProjectSubTaskService keeps it as stored.
		public string TimeOffType { get; set; }
	}

	internal static class TimeOffTypes
	{
		public const string LunchBreak = "lunchBreak";
		public const string StatHoliday = "statHoliday";
		public const string Vacation = "vacation";
		public const string PersonalLeave = "personalLeave";
		public const string SickLeave = "sickLeave";
		// Leave without pay: on the paid hours off project all the same, so the
		// day is accounted for, but kept apart from paid leave.
		public const string UnpaidLeave = "unpaidLeave";
	}
}
