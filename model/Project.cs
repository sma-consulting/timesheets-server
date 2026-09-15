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

		[BsonRepresentation(BsonType.ObjectId)]
		public string CustomerId { get; set; }

		public string Name { get; set; }

		public string ShortName { get; set; }

        public string FundingCode { get; set; }

		public bool HighPriority { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
		public string ProjectManagerId { get; set; }

		public decimal TotalBudget { get; set; }

		public List<ProjectPhase> Phases { get; set; } = new List<ProjectPhase>();
	}
}
