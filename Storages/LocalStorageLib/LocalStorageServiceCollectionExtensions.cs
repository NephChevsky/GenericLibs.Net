using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StorageLib;

namespace LocalStorageLib
{
	public static class LocalStorageServiceCollectionExtensions
	{
		/// <summary>Registers <see cref="IStorageService"/> backed by the file system, configured from the "LocalStorage" section.</summary>
		public static IServiceCollection AddLocalStorageLib(this IServiceCollection services, IConfiguration configuration)
		{
			var options = configuration.GetSection(LocalStorageOptions.SectionName).Get<LocalStorageOptions>() ?? new LocalStorageOptions();
			services.TryAddSingleton<IStorageService>(_ => new LocalStorageService(options));
			return services;
		}
	}
}
