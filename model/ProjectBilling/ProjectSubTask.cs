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
	}
}
