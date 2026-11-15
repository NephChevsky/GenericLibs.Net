using System.Runtime.CompilerServices;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using StorageLib;

namespace AzureBlobLib
{
	public class AzureBlobStorageService : IStorageService
	{
		private readonly BlobServiceClient _client;
		private readonly bool _createContainer;

		public AzureBlobStorageService(BlobServiceClient client, bool createContainerIfNotExists = true)
		{
			_client = client;
			_createContainer = createContainerIfNotExists;
		}

		public async Task UploadAsync(string container, string path, Stream content, string? contentType = null, bool overwrite = true, CancellationToken cancellationToken = default)
		{
			var containerClient = _client.GetBlobContainerClient(container);
			if (_createContainer)
			{
				await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
			}

			var options = new BlobUploadOptions();
			if (contentType != null)
			{
				options.HttpHeaders = new BlobHttpHeaders { ContentType = contentType };
			}
			if (!overwrite)
			{
				options.Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All };
			}
			await containerClient.GetBlobClient(path).UploadAsync(content, options, cancellationToken);
		}

		public async Task<Stream?> OpenReadAsync(string container, string path, CancellationToken cancellationToken = default)
		{
			var blob = _client.GetBlobContainerClient(container).GetBlobClient(path);
			try
			{
				return await blob.OpenReadAsync(cancellationToken: cancellationToken);
			}
			catch (RequestFailedException ex) when (ex.Status == 404)
			{
				return null;
			}
		}

		public async Task<bool> ExistsAsync(string container, string path, CancellationToken cancellationToken = default)
		{
			var response = await _client.GetBlobContainerClient(container).GetBlobClient(path).ExistsAsync(cancellationToken);
			return response.Value;
		}

		public async Task<bool> DeleteAsync(string container, string path, CancellationToken cancellationToken = default)
		{
			var response = await _client.GetBlobContainerClient(container).GetBlobClient(path).DeleteIfExistsAsync(cancellationToken: cancellationToken);
			return response.Value;
		}

		public async IAsyncEnumerable<string> ListAsync(string container, string? prefix = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			var containerClient = _client.GetBlobContainerClient(container);
			await foreach (var item in containerClient.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, cancellationToken))
			{
				yield return item.Name;
			}
		}
	}
}
