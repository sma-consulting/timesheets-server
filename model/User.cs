using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class User : GeneralModel
	{
		public string Email { get; set; }

		[BsonRepresentation(BsonType.ObjectId)]
		public string TeamMemberId { get; set; }
	}
}
