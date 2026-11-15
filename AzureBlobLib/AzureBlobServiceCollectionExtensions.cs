using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StorageLib;

namespace AzureBlobLib
{
	public static class AzureBlobServiceCollectionExtensions
	{
		/// <summary>Registers <see cref="IStorageService"/> backed by Azure Blob Storage, configured from the "AzureBlob" section.</summary>
		public static IServiceCollection AddAzureBlobLib(this IServiceCollection services, IConfiguration configuration)
		{
			var options = configuration.GetSection(AzureBlobOptions.SectionName).Get<AzureBlobOptions>() ?? new AzureBlobOptions();
			if (string.IsNullOrWhiteSpace(options.ConnectionString))
			{
				throw new InvalidOperationException($"{AzureBlobOptions.SectionName}:ConnectionString is not configured.");
			}

			services.TryAddSingleton(_ => new BlobServiceClient(options.ConnectionString));
			services.TryAddSingleton<IStorageService>(sp =>
				new AzureBlobStorageService(sp.GetRequiredService<BlobServiceClient>(), options.CreateContainerIfNotExists));
			return services;
		}
	}
}
