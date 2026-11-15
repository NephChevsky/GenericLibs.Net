using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DatabaseLib
{
	/// <summary>
	/// Provider-agnostic design-time factory (used by EF Core migrations tooling).
	/// </summary>
	public abstract class DbContextFactoryBase<TContext>(IDatabaseProvider provider) : IDesignTimeDbContextFactory<TContext> where TContext : DbContext
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

			var connectionString = config.GetConnectionString(provider.ConnectionStringName);

			var optionsBuilder = new DbContextOptionsBuilder<TContext>();
			provider.Configure(optionsBuilder, connectionString!);

			return CreateDbContextInstance(optionsBuilder.Options);
		}

		protected abstract TContext CreateDbContextInstance(DbContextOptions<TContext> options);
	}
}
