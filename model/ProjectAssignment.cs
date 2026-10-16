using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	// Who is working on what, in which role, and for how long. Top-level rather
	// than embedded in Project so it can be queried from either direction - the
	// members of a project, or the work a team member is assigned to.
	internal class ProjectAssignment : GeneralModel
	{
		[BsonRepresentation(BsonType.ObjectId)]
		public string ProjectId { get; set; }

		[BsonRepresentation(BsonType.ObjectId)]
		public string ProjectRoleId { get; set; }

		// Reserved. Assignment is project-level for now - you are put on a project
		// and pick your own sub-task when logging time - so nothing sets or reads
		// this. Kept so sub-task-level assignment can be turned back on later
		// without a schema change.
		[BsonRepresentation(BsonType.ObjectId)]
		public string ProjectSubTaskId { get; set; }

		[BsonRepresentation(BsonType.ObjectId)]
		public string TeamMemberId { get; set; }

		public DateOnly StartDate { get; set; }

		// Null while the assignment is open-ended.
		public DateOnly? EndDate { get; set; }
	}
}
