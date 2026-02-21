using Microsoft.EntityFrameworkCore;
using PostgresLib.Interfaces;
using System.Linq.Expressions;

namespace PostgresLib
{
	public class GenericDbContext<TContext>(DbContextOptions<TContext> options) : DbContext(options) where TContext : DbContext
	{
		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			// Automatically apply generic field configurations for all entity types
			foreach (var entityType in modelBuilder.Model.GetEntityTypes())
			{
				var clrType = entityType.ClrType;
				if (clrType == null) continue;

				// Configure Id property
				var idProperty = entityType.FindProperty("Id");
				if (idProperty != null)
				{
					modelBuilder.Entity(clrType).Property("Id")
						.ValueGeneratedOnAdd()
						.IsRequired();
				}

				// Configure datetime tracking properties
				if (typeof(IDateTimeTrackable).IsAssignableFrom(clrType))
				{
					modelBuilder.Entity(clrType).Property("CreatedAt").IsRequired();
					modelBuilder.Entity(clrType).Property("UpdatedAt");
				}

				// Configure soft delete property
				if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
				{
					modelBuilder.Entity(clrType).Property("IsDeleted")
						.IsRequired()
						.HasDefaultValue(false);
				}
			}

			// Automatically apply soft delete query filters
			ApplySoftDeleteQueryFilters(modelBuilder);
		}

		public override int SaveChanges()
		{
			ApplySoftDelete();
			ApplyDateTimeTracking();
			return base.SaveChanges();
		}

		public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
		{
			ApplySoftDelete();
			ApplyDateTimeTracking();
			return base.SaveChangesAsync(cancellationToken);
		}

		public int HardDelete<TEntity>(TEntity entity) where TEntity : class
		{
			Entry(entity).State = EntityState.Deleted;
			return base.SaveChanges();
		}

		public Task<int> HardDeleteAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default) where TEntity : class
		{
			Entry(entity).State = EntityState.Deleted;
			return base.SaveChangesAsync(cancellationToken);
		}

		protected void ApplySoftDeleteQueryFilters(ModelBuilder modelBuilder)
		{
			foreach (var entityType in modelBuilder.Model.GetEntityTypes())
			{
				var clrType = entityType.ClrType;
				if (clrType == null)
				{
					continue;
				}

				if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
				{
					var parameter = Expression.Parameter(clrType, "e");
					var isDeletedProperty = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
					var notDeleted = Expression.Not(isDeletedProperty);
					var lambdaType = typeof(Func<,>).MakeGenericType(clrType, typeof(bool));
					var lambda = Expression.Lambda(lambdaType, notDeleted, parameter);

					modelBuilder.Entity(clrType).HasQueryFilter(lambda);
				}
			}
		}

		private void ApplySoftDelete()
		{
			foreach (var entry in ChangeTracker.Entries())
			{
				if (entry.State == EntityState.Deleted && entry.Entity is ISoftDeletable softDeletable)
				{
					softDeletable.IsDeleted = true;
					entry.State = EntityState.Modified;
				}
			}
		}

		private void ApplyDateTimeTracking()
		{
			var now = DateTime.UtcNow;

			foreach (var entry in ChangeTracker.Entries())
			{
				if (entry.Entity is IDateTimeTrackable trackable)
				{
					if (entry.State == EntityState.Added)
					{
						trackable.CreatedAt = now;
						trackable.UpdatedAt = now;
					}
					else if (entry.State == EntityState.Modified)
					{
						trackable.UpdatedAt = now;
					}
				}
			}
		}
	}
}
