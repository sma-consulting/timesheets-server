using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using MongoDB.Driver;
using System;
using System.IO;

namespace sma.plan
{
	// Receipt details in the database, receipt files in blob storage.
	//
	// The two stores can't be written together, so the file goes first: a
	// failed save leaves at most a stray blob, never a receipt with no file.
	internal class ExpenseReceiptRepo : DatabaseRepo<ExpenseReceipt>, IExpenseReceiptRepo
	{
		private readonly BlobContainerClient _container;

		public ExpenseReceiptRepo(BlobContainerClient container)
		{
			_container = container;
		}

		public new ExpenseReceipt Create(ExpenseReceipt data)
		{
			string blobName = Guid.NewGuid().ToString("N") + Path.GetExtension(data.FileName ?? "");
			BlobClient blob = _container.GetBlobClient(blobName);

			using (var content = new MemoryStream(data.Data))
			{
				blob.Upload(content, new BlobUploadOptions
				{
					HttpHeaders = new BlobHttpHeaders
					{
						ContentType = data.ContentType ?? "application/octet-stream",
					},
				});
			}

			data.BlobName = blobName;
			data.Data = null;

			try
			{
				return base.Create(data);
			}
			catch
			{
				blob.DeleteIfExists();
				throw;
			}
		}

		public new ExpenseReceipt Delete(string id)
		{
			ExpenseReceipt deleted = base.Delete(id);
			if (!string.IsNullOrWhiteSpace(deleted?.BlobName))
			{
				_container.GetBlobClient(deleted.BlobName).DeleteIfExists();
			}
			return deleted;
		}

		public ExpenseReceipt Find(string id)
		{
			return string.IsNullOrWhiteSpace(id)
				? null
				: DBCollection.Find(r => r.Id == id).FirstOrDefault();
		}

		public Stream OpenRead(ExpenseReceipt receipt)
		{
			if (!string.IsNullOrWhiteSpace(receipt.BlobName))
			{
				try
				{
					return _container.GetBlobClient(receipt.BlobName).OpenRead();
				}
				catch (RequestFailedException e) when (e.Status == 404)
				{
					// The record outlived its file (deleted from storage, or saved
					// against other storage): treated as no receipt, not a crash.
					return null;
				}
			}

			// Saved before blob storage: the bytes are still on the document.
			return receipt.Data != null ? new MemoryStream(receipt.Data) : null;
		}
	}
}
