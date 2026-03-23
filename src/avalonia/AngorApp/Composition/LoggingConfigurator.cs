using System;
using System.IO;
using Angor.Sdk.Common;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;

namespace AngorApp.Composition;

public static class LoggingConfigurator
{
    private const string LogLevelEnvironmentVariable = "ANGOR_LOG_LEVEL";

    public static ILogger CreateLogger(string appName, IApplicationStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);

        var logsDirectory = TryGetLogsDirectory(storage, appName);
        var minimumLevel = ResolveMinimumLevel();

        var logger = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
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

    private static string TryGetLogsDirectory(IApplicationStorage storage, string appName)
    {
        try
        {
            return storage.GetLogsDirectory(appName);
        }
        catch (Exception)
        {
            var fallback = Path.Combine(AppContext.BaseDirectory, "Logs");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }

    private static LogEventLevel ResolveMinimumLevel()
    {
#if DEBUG
        return LogEventLevel.Debug;
#else
        var candidate = Environment.GetEnvironmentVariable(LogLevelEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(candidate) &&
            Enum.TryParse(candidate, ignoreCase: true, out LogEventLevel parsed))
        {
            return parsed;
        }

        return LogEventLevel.Information;
#endif
    }
}
