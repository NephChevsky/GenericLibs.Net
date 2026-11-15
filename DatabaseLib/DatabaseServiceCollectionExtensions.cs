using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DatabaseLib
{
	public static class DatabaseServiceCollectionExtensions
	{
		public static IServiceCollection AddDatabaseLib<T>(this IServiceCollection services, IConfiguration configuration, IDatabaseProvider provider) where T : DbContext
		{
			string connectionString = configuration.GetConnectionString(provider.ConnectionStringName)
				?? throw new InvalidOperationException($"Connection string '{provider.ConnectionStringName}' was not found");

			services.AddDbContext<T>(options => provider.Configure(options, connectionString));

			services.AddHttpContextAccessor();

			return services;
		}
	}
}
