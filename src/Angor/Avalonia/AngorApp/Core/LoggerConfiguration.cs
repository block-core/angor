using System;
using System.IO;
using Angor.Shared.Utilities;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace AngorApp.Core;

public static class LoggerConfig
{
    public static ILoggingBuilder RegisterSerilog(this ILoggingBuilder builder)
    {
        var logsDirectory = ApplicationStoragePaths
            .GetLogsDirectory("Angor")
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

        builder.AddSerilog(logger);

        return builder;
    }
}