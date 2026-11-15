using DatabaseLib;
using Microsoft.EntityFrameworkCore;

namespace PostgresLib
{
	public abstract class GenericDbContextFactory<TContext>() : DbContextFactoryBase<TContext>(new PostgresDatabaseProvider()) where TContext : DbContext
	{
	}
}
