namespace StorageLib
{
	public interface IStorageService
	{
		/// <summary>Uploads <paramref name="content"/> to <paramref name="path"/> in <paramref name="container"/>.</summary>
		Task UploadAsync(string container, string path, Stream content, string? contentType = null, bool overwrite = true, CancellationToken cancellationToken = default);

		/// <summary>Opens a readable stream on the item, or returns null if it does not exist. The caller disposes the stream.</summary>
		Task<Stream?> OpenReadAsync(string container, string path, CancellationToken cancellationToken = default);

		Task<bool> ExistsAsync(string container, string path, CancellationToken cancellationToken = default);

		/// <summary>Deletes the item. Returns true if it existed.</summary>
		Task<bool> DeleteAsync(string container, string path, CancellationToken cancellationToken = default);

		/// <summary>Lists item paths (using '/' separators) in the container, optionally filtered by prefix.</summary>
		IAsyncEnumerable<string> ListAsync(string container, string? prefix = null, CancellationToken cancellationToken = default);
	}
}
