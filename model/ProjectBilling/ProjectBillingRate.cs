using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace sma.plan
{
	// What one person bills at on one rate list, over a date range. The same
	// person can bill differently on two lists, and a rate change is a new row,
	// so an hour is priced by the row covering the day it was worked - from the
	// list of the project it was worked on.
	//
	// Per list + person the rows are contiguous - no overlaps, no gaps - and only
	// the latest may be open-ended. ProjectBillingRateFunctions enforces that.
	internal class ProjectBillingRate : GeneralModel
	{
		[BsonRepresentation(BsonType.ObjectId)]
		public string RateListId { get; set; }

		[BsonRepresentation(BsonType.ObjectId)]
		public string TeamMemberId { get; set; }

		// Not stored with the list's rows: set when they're read for a project
		// (see IProjectBillingRateRepo.GetForProjects), so an invoice billing
		// several projects can tell each one's apart, and kept on its copies.
		[BsonRepresentation(BsonType.ObjectId)]
		[BsonIgnoreIfNull]
		public string ProjectId { get; set; }

		public decimal HourlyRate { get; set; }

		public DateOnly StartDate { get; set; }

		// Null while this is the current rate.
		public DateOnly? EndDate { get; set; }
	}
}
