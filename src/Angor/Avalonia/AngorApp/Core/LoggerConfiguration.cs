using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace AngorApp.Core;

public class LoggerConfig
{
    public static ILoggerFactory CreateFactory()
    {
        // Configurar Serilog
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        // Crear el factory
        var factory = new LoggerFactory();
        
        // Agregar Serilog al factory
        factory.AddSerilog(logger);

        return factory;
    }

    public static ILogger<T> CreateLogger<T>()
    {
        return CreateFactory().CreateLogger<T>();
    }
}