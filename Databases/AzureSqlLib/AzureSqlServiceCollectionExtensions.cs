using DatabaseLib;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AzureSqlLib
{
	public static class AzureSqlServiceCollectionExtensions
	{
		public static IServiceCollection AddAzureSqlLib<T>(this IServiceCollection services, IConfiguration configuration) where T : DbContext
		{
			return services.AddDatabaseLib<T>(configuration, new AzureSqlDatabaseProvider());
		}
	}
}
