using Microsoft.Extensions.Hosting;
using Serilog;

namespace SerilogLib
{
	public static class SerilogHostBuilderExtensions
	{
		public static IHostBuilder UseSerilog(this IHostBuilder hostBuilder)
		{
			Log.Logger = new LoggerConfiguration()
				.MinimumLevel.Information()
				.Enrich.FromLogContext()
				.WriteTo.Console()
				.WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
				.CreateLogger();

			return hostBuilder.UseSerilog();
		}
	}
}
