using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PostgresLib.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Linq.Expressions;

namespace PostgresLib
{
	public class GenericDbContext<TContext>(DbContextOptions<TContext> options, IHttpContextAccessor? httpContextAccessor = null) : DbContext(options) where TContext : DbContext
	{
		private readonly IHttpContextAccessor? _httpContextAccessor = httpContextAccessor;

		// Property that evaluates at query execution time, not model creation time
		protected Guid CurrentUserId => GetCurrentUserId();

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

				// Configure owner property
				if (typeof(IOwnable).IsAssignableFrom(clrType))
				{
					modelBuilder.Entity(clrType).Property("Owner").IsRequired();
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

				// Automatically apply owner query filters
				ApplyOwnerQueryFilters(modelBuilder);
			}

		public override int SaveChanges()
		{
			ApplySoftDelete();
			ApplyDateTimeTracking();
			ApplyOwnerTracking();
			return base.SaveChanges();
		}

		public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
		{
			ApplySoftDelete();
			ApplyDateTimeTracking();
			ApplyOwnerTracking();
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

		protected void ApplyOwnerQueryFilters(ModelBuilder modelBuilder)
		{
			foreach (var entityType in modelBuilder.Model.GetEntityTypes())
			{
				var clrType = entityType.ClrType;
				if (clrType == null)
				{
					continue;
				}

				if (typeof(IOwnable).IsAssignableFrom(clrType))
				{
					var parameter = Expression.Parameter(clrType, "e");
					var ownerProperty = Expression.Property(parameter, nameof(IOwnable.Owner));

					var currentUserIdMethod = typeof(GenericDbContext<TContext>).GetMethod(nameof(GetCurrentUserId), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
					var thisConstant = Expression.Constant(this);
					var currentUserIdCall = Expression.Call(thisConstant, currentUserIdMethod!);
					var ownerEquals = Expression.Equal(ownerProperty, currentUserIdCall);

					var lambdaType = typeof(Func<,>).MakeGenericType(clrType, typeof(bool));
					var lambda = Expression.Lambda(lambdaType, ownerEquals, parameter);

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
			DateTime now = DateTime.UtcNow;

			foreach (var entry in ChangeTracker.Entries())
			{
				if (entry.Entity is IDateTimeTrackable trackable)
				{
					if (entry.State == EntityState.Added)
					{
						if (trackable.CreatedAt == default)
						{
							trackable.CreatedAt = now;
						}
						trackable.UpdatedAt = now;
					}
					else if (entry.State == EntityState.Modified)
					{
						trackable.UpdatedAt = now;
					}
				}
			}
		}

		private void ApplyOwnerTracking()
		{
			foreach (var entry in ChangeTracker.Entries())
			{
				if (entry.State == EntityState.Added && entry.Entity is IOwnable ownable)
				{
					// GetCurrentUserId() will throw if user is not authenticated
					ownable.Owner = GetCurrentUserId();
				}
			}
		}

		private Guid GetCurrentUserId()
		{
			// Design-time scenario (migrations) - accessor not injected
			if (_httpContextAccessor == null)
				return Guid.Empty;

			var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

			if (Guid.TryParse(userIdClaim, out var userId))
				return userId;

			throw new InvalidOperationException("User is not authenticated or user ID claim is missing. IOwnable entities require an authenticated user.");
		}
	}
}
