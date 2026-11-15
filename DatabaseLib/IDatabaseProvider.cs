using Microsoft.EntityFrameworkCore;

namespace DatabaseLib
{
	/// <summary>
	/// Abstraction over a concrete database provider (PostgreSQL, Azure SQL, ...).
	/// </summary>
	public interface IDatabaseProvider
	{
		/// <summary>Name of the entry in the ConnectionStrings configuration section.</summary>
		string ConnectionStringName { get; }

		/// <summary>Configures the EF Core provider (and provider-specific conventions) on the options builder.</summary>
		void Configure(DbContextOptionsBuilder options, string connectionString);
	}
}
