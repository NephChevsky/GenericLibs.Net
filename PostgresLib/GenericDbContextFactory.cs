using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace PostgresLib
{
	public abstract class GenericDbContextFactory<TContext> : IDesignTimeDbContextFactory<TContext> where TContext : DbContext
	{
		public TContext CreateDbContext(string[] args)
		{
			var basePath = Directory.GetCurrentDirectory();

			var config = new ConfigurationBuilder()
				.SetBasePath(basePath)
				.AddJsonFile("appsettings.json", optional: true)
				.AddJsonFile("appsettings.Development.json", optional: true)
				.AddEnvironmentVariables()
				.AddUserSecrets(typeof(TContext).Assembly, optional: true)
				.Build();

			var connectionString = config.GetConnectionString("Postgres");

			var optionsBuilder = new DbContextOptionsBuilder<TContext>();
			optionsBuilder.UseNpgsql(connectionString)
				.UseSnakeCaseNamingConvention();

			return CreateDbContextInstance(optionsBuilder.Options);
		}

		protected abstract TContext CreateDbContextInstance(DbContextOptions<TContext> options);
	}
}
