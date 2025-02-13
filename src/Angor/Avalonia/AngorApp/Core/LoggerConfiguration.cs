using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace AngorApp.Core;

public static class LoggerConfig
{
    public static ILoggingBuilder RegisterSerilog(this ILoggingBuilder builder)
    {
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        builder.AddSerilog(logger);

        return builder;
    }
}