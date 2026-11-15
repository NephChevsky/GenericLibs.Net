using DatabaseLib;
using Microsoft.EntityFrameworkCore;

namespace AzureSqlLib
{
	public class AzureSqlDatabaseProvider : IDatabaseProvider
	{
		public string ConnectionStringName => "AzureSql";

		public void Configure(DbContextOptionsBuilder options, string connectionString)
		{
			options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
		}
	}
}
