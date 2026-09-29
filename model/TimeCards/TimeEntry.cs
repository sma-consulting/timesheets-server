using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class TimeEntry : GeneralModel
	{
		public TimeEntry() { }

		[BsonRepresentation(BsonType.ObjectId)]
		public string CustomerId { get; set; }

		[BsonRepresentation(BsonType.ObjectId)]
		public string ProjectId { get; set; }

		[BsonRepresentation(BsonType.ObjectId)]
		public string ProjectTaskId { get; set; }

		[BsonRepresentation(BsonType.ObjectId)]
		public string ProjectSubTaskId { get; set; }

		[BsonRepresentation(BsonType.ObjectId)]
		public string TeamMemberId { get; set; }

		public DateOnly Date { get; set; }

		public decimal Hours { get; set; }

		public string Notes { get; set; }

		public bool Billable { get; set; }

		public DateTime? LastModified { get; set; }

		public string ModifiedBy { get; set; }

		// Earlier versions of this entry, oldest first. Kept by the server only:
		// TimeEntryService carries it across updates, so a client can't write or
		// erase it. Snapshots have none of their own, hence IgnoreIfNull.
		[BsonIgnoreIfNull]
		public List<TimeEntry> History { get; set; } = new List<TimeEntry>();

		//Get a clone of data for History, omitting the audit log itself
		public TimeEntry AuditClone()
		{
			return new TimeEntry
			{
				CustomerId = this.CustomerId,
				ProjectId = this.ProjectId,
				ProjectTaskId = this.ProjectTaskId,
				ProjectSubTaskId = this.ProjectSubTaskId,
				TeamMemberId = this.TeamMemberId,
				Date = this.Date,
				Hours = this.Hours,
				Notes = this.Notes,
				Billable = this.Billable,
				LastModified = this.LastModified,
				ModifiedBy = this.ModifiedBy,
				History = null,
			};
		}

		// Whether the parts a person can change are the same - not the audit
		// fields. An unchanged save (the week grid saves on every blur) shouldn't
		// add a history item.
		public bool SameAs(TimeEntry other)
		{
			return other != null
				&& CustomerId == other.CustomerId
				&& ProjectId == other.ProjectId
				&& ProjectTaskId == other.ProjectTaskId
				&& ProjectSubTaskId == other.ProjectSubTaskId
				&& TeamMemberId == other.TeamMemberId
				&& Date == other.Date
				&& Hours == other.Hours
				&& (Notes ?? "") == (other.Notes ?? "")
				&& Billable == other.Billable;
		}
	}
}
