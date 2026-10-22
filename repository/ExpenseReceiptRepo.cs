using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
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

		// A SAS link: read-only, for this one file, until `lifetime` is up. The
		// browser then opens it straight from storage, which names it and shows
		// it by its type (a PDF in the browser's own viewer).
		public Uri ReadLink(ExpenseReceipt receipt, TimeSpan lifetime)
		{
			if (string.IsNullOrWhiteSpace(receipt.BlobName))
			{
				return null;
			}

			BlobClient blob = _container.GetBlobClient(receipt.BlobName);
			DateTimeOffset expires = DateTimeOffset.UtcNow.Add(lifetime);

			var sas = new BlobSasBuilder
			{
				BlobContainerName = _container.Name,
				BlobName = receipt.BlobName,
				Resource = "b",
				ExpiresOn = expires,
				// Shown in the tab, and saved under, the name it was uploaded with.
				ContentDisposition = $"inline; filename=\"{(receipt.FileName ?? receipt.BlobName).Replace("\"", "")}\"",
			};
			sas.SetPermissions(BlobSasPermissions.Read);

			// With an account key (a connection string) the link is signed with it.
			if (blob.CanGenerateSasUri)
			{
				return blob.GenerateSasUri(sas);
			}

			// Without one - an Azure login, as here and on the Function App - it's
			// signed with a key Azure lends that login for the link's lifetime.
			BlobServiceClient service = _container.GetParentBlobServiceClient();
			UserDelegationKey key = service.GetUserDelegationKey(new BlobGetUserDelegationKeyOptions(expires)).Value;
			return new BlobUriBuilder(blob.Uri)
			{
				Sas = sas.ToSasQueryParameters(key, service.AccountName),
			}.ToUri();
		}
	}
}
