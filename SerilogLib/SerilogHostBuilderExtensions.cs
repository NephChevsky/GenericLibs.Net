using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace SerilogLib
{
	public static class SerilogHostBuilderExtensions
	{
		public static IHostBuilder UseSerilogLib(this IHostBuilder hostBuilder, bool writeToConsole = true, bool writeToFile = true)
		{
			Log.Logger = CreateLogger(writeToConsole, writeToFile);

			return hostBuilder.UseSerilog();
		}

		public static IHostApplicationBuilder UseSerilogLib(this IHostApplicationBuilder builder, bool writeToConsole = true, bool writeToFile = true)
		{
			Log.Logger = CreateLogger(writeToConsole, writeToFile);
			builder.Services.AddSerilog();

			return builder;
		}

		private static Serilog.ILogger CreateLogger(bool writeToConsole, bool writeToFile)
		{
			LoggerConfiguration configuration = new LoggerConfiguration()
				.MinimumLevel.Information()
				.Enrich.FromLogContext();

			if (writeToConsole)
			{
				configuration = configuration.WriteTo.Console();
			}

			if (writeToFile)
			{
				configuration = configuration.WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day);
			}

			return configuration.CreateLogger();
		}
	}
}
