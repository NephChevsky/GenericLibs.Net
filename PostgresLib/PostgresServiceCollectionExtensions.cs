using DatabaseLib;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PostgresLib
{
public static class PostgresServiceCollectionExtensions
{
public static IServiceCollection AddPostgresLib<T>(this IServiceCollection services, IConfiguration configuration) where T : DbContext
{
return services.AddDatabaseLib<T>(configuration, new PostgresDatabaseProvider());
}
}
}
