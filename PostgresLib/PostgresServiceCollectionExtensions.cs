using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PostgresLib
{
	public static class PostgresServiceCollectionExtensions
	{
		public static IServiceCollection AddPostgresDb<T>(this IServiceCollection services, IConfiguration configuration) where T : DbContext
		{
			string connectionString = configuration.GetConnectionString("Postgres") ?? throw new InvalidOperationException("Connection string 'Postgres' was not found");
			
			services.AddDbContext<T>(options =>
			{
				options.UseNpgsql(connectionString)
					.UseSnakeCaseNamingConvention();
			});

			return services;
		}
	}
}
