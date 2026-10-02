using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{

	internal class ProjectTask : GeneralModel
	{
		[BsonRepresentation(BsonType.ObjectId)]
		public string CustomerId { get; set; }

		[BsonRepresentation(BsonType.ObjectId)]
		public string ProjectId { get; set; }

		public string Name { get; set; }

		public string ShortName { get; set; }

		// No FundingCode: only the project has a code (Project.FundingCode).
		// No Billable here: whether time is billable is set on each sub-task
		// (ProjectSubTask.Billable), the level the invoice prices at. Tasks saved
		// with the old field still load - GeneralModel ignores extra elements.
	}
}
