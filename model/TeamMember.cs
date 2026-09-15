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

		public string Email { get; set; }

		public List<string> AssignedRoles { get; set; } = new List<string>();

		public DateOnly StartDate { get; set; }

		public DateOnly EndDate { get; set; }

		public int PercentAvailable { get; set; }
	}
}
