using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace sma.plan
{
	// The receipt for one expense: a photo or PDF. The file itself lives in
	// blob storage (BlobName); this document only describes it. Kept apart from
	// Expense so the list of expenses stays light.
	internal class ExpenseReceipt : GeneralModel
	{
		// Plenty for a phone photo or a scanned PDF.
		public const long MaxBytes = 10 * 1024 * 1024;

		// The file's name in the receipts container.
		public string BlobName { get; set; }

		// Who uploaded it, for access checks.
		[BsonRepresentation(BsonType.ObjectId)]
		public string TeamMemberId { get; set; }

		public string FileName { get; set; }

		public string ContentType { get; set; }

		// The file's bytes. Only on the way in, for the repo to upload - it is
		// never saved with new receipts. Receipts from before blob storage still
		// have it, and are served from it.
		public byte[] Data { get; set; }

		public DateTime UploadedAt { get; set; }
	}
}
