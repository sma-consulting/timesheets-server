using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{

	/**
	 * Slightly clunky API to accomodate human friendly data model.
	 */
	internal class TimeCard: GeneralModel
	{
		private List<TimeCardWeek>? _weeks;

		public TimeCard() { }

		[BsonRepresentation(BsonType.ObjectId)]
		public string TeamMemberId { get; set; }

		public DateOnly Month { get; set; }

		public TimeCardWeek WeekOne { get; set; } = new TimeCardWeek();

		public TimeCardWeek WeekTwo { get; set; } = new TimeCardWeek();

		public TimeCardWeek WeekThree { get; set; } = new TimeCardWeek();

		public TimeCardWeek WeekFour { get; set; } = new TimeCardWeek();

		public TimeCardWeek WeekFive { get; set; } = new TimeCardWeek();

		public TimeCardWeek WeekSix { get; set; } = new TimeCardWeek();

		[BsonIgnore]
		//not threadsafe
		public List<TimeCardWeek> Weeks
		{
			get
			{
				return _weeks ?? new List<TimeCardWeek> { WeekOne, WeekTwo, WeekThree, WeekFour, WeekFive, WeekSix };
			}
		}
	}
}
