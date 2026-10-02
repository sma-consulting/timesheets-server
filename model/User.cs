using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	// A login. The TeamMember representing this person points back here via its
	// UserId - not every TeamMember has one, since planning placeholders never
	// sign in.
	internal class User : GeneralModel
	{
		// Values that may appear in Privileges.
		public const string AdminPrivilege = "admin";

		public string Email { get; set; }

		public string DisplayName { get; set; }

		public List<string> Privileges { get; set; } = new List<string>();
	}
}
