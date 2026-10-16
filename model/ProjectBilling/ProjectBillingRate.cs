using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	// What a role bills at on one project, over a date range. The same role can
	// bill differently on two projects, and a rate change is a new row, so an hour
	// is priced by the row covering the day it was worked.
	//
	// Per project + role the rows are contiguous - no overlaps, no gaps - and only
	// the latest may be open-ended. ProjectBillingRateFunctions enforces that.
	internal class ProjectBillingRate : GeneralModel
	{

		[BsonRepresentation(BsonType.ObjectId)]
		public string ProjectId { get; set; }

		[BsonRepresentation(BsonType.ObjectId)]
		public string ProjectRoleId { get; set; }

		public decimal HourlyRate { get; set; }

		public DateOnly StartDate { get; set; }

		// Null while this is the current rate.
		public DateOnly? EndDate { get; set; }
	}
}
