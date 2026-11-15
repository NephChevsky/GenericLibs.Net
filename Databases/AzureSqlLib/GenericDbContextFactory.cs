using DatabaseLib;
using Microsoft.EntityFrameworkCore;

namespace AzureSqlLib
{
	public abstract class GenericDbContextFactory<TContext>() : DbContextFactoryBase<TContext>(new AzureSqlDatabaseProvider()) where TContext : DbContext
	{
	}
}
