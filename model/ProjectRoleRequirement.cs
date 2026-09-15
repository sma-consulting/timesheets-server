using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class ProjectRoleRequirement
	{
        [BsonRepresentation(BsonType.ObjectId)]
        public string RoleId { get; set; }

		public decimal RequiredAllocationPercentage { get; set; }

		public UtilizationType Utilization { get; set; }

		[BsonRepresentation(BsonType.ObjectId)]
		public string LoadingCurveId { get; set; }
	}
}
