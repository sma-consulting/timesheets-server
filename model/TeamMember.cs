using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class TeamMember : GeneralModel
	{
		public string Name { get; set; }

		// Null for planning placeholders ("Summer Co-op 2028") - they are staffable
		// but nobody signs in as them. Email lives on User, since it is a login
		// credential and a placeholder has no login.
		[BsonRepresentation(BsonType.ObjectId)]
		public string UserId { get; set; }

		public string Description { get; set; }

		public List<string> AssignedRoles { get; set; } = new List<string>();

		public int PercentAvailable { get; set; }
	}
}
