using System.IO;
using Angor.Shared.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;

namespace AngorApp.Composition;

public static class LoggingConfigurator
{
    public static ILogger CreateLogger(string appName)
    {
        var logsDirectory = ApplicationStoragePaths
            .GetLogsDirectory(appName)
            .OnFailureCompensate(_ => Result.Try(() =>
            {
                var fallback = Path.Combine(AppContext.BaseDirectory, "Logs");
                Directory.CreateDirectory(fallback);
                return fallback;
            }))
            .Value;

        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: Path.Combine(logsDirectory, "angor-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 15,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Logger = logger;
        return logger;
    }

    public static void RegisterLogger(IServiceCollection services, ILogger logger)
    {
        services.AddLogging(builder => builder.AddSerilog(logger));
        services.AddSingleton(logger);
    }
}
