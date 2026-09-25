using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace sma.plan
{
	// The receipt file for one expense: a photo or PDF, stored as bytes. Kept
	// apart from Expense so the list of expenses stays light, and only fetched
	// when someone opens the receipt.
	internal class ExpenseReceipt : GeneralModel
	{
		// Well under MongoDB's 16 MB document limit, and plenty for a phone photo.
		public const long MaxBytes = 10 * 1024 * 1024;

		// Who uploaded it, for access checks.
		[BsonRepresentation(BsonType.ObjectId)]
		public string TeamMemberId { get; set; }

		public string FileName { get; set; }

		public string ContentType { get; set; }

		public byte[] Data { get; set; }

		public DateTime UploadedAt { get; set; }
	}
}
