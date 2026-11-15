using DatabaseLib;
using Microsoft.EntityFrameworkCore;

namespace PostgresLib
{
	public class PostgresDatabaseProvider : IDatabaseProvider
	{
		public string ConnectionStringName => "Postgres";

		public void Configure(DbContextOptionsBuilder options, string connectionString)
		{
			options.UseNpgsql(connectionString)
				.UseSnakeCaseNamingConvention();
		}
	}
}
