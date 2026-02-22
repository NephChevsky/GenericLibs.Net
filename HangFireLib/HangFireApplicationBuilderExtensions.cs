using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Linq.Expressions;
using System.Reflection;

namespace HangFireLib
{
	public static class HangFireApplicationBuilderExtensions
	{
		public static IApplicationBuilder UseHangFireLib<T>(this IApplicationBuilder app, Action<IRecurringJobManager>? configureRecurringJobs = null)
			where T : class
		{
			using var scope = app.ApplicationServices.CreateScope();
			var recurringJobs = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

			// Check if the service is registered in DI
			var service = scope.ServiceProvider.GetService<T>();
			if (service != null)
			{
				RegisterRecurringJobsFromType<T>(recurringJobs);
			}

			// Allow additional manual configuration
			configureRecurringJobs?.Invoke(recurringJobs);

			return app;
		}

		private static void RegisterRecurringJobsFromType<T>(IRecurringJobManager recurringJobs) where T : class
		{
			var type = typeof(T);
			var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
				.Where(m => m.GetCustomAttribute<RecurringJobAttribute>() != null);

			foreach (var method in methods)
			{
				var attribute = method.GetCustomAttribute<RecurringJobAttribute>();
				if (attribute == null) continue;

				// Use method name as JobId if not explicitly provided
				var jobId = string.IsNullOrEmpty(attribute.JobId) ? method.Name : attribute.JobId;

				// Build expression and register job
				var parameter = Expression.Parameter(typeof(T), "x");
				var methodCall = Expression.Call(parameter, method);
				var lambda = Expression.Lambda<Action<T>>(methodCall, parameter);

				recurringJobs.AddOrUpdate(jobId, lambda, attribute.CronExpression);
			}
		}
	}
}
