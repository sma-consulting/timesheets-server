using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class ProjectBillingRate
	{

		[BsonRepresentation(BsonType.ObjectId)]
		public string ProjectId { get; set; }

		[BsonRepresentation(BsonType.ObjectId)]
		public string ProjectRoleId { get; set; }

		public decimal HourlyRate { get; set; }

		public DateOnly StartDate { get; set; }

		public DateOnly EndDate { get; set; }
	}
}
