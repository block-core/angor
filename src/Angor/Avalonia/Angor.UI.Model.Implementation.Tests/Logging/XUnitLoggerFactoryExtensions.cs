using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Angor.UI.Model.Implementation.Tests.Logging;

public static class XUnitLoggerFactoryExtensions
{
    public static ILoggingBuilder AddXUnitLogger(
        this ILoggingBuilder builder,
        ITestOutputHelper outputHelper,
        LogLevel minLevel = LogLevel.Trace)
    {
        // Podrías implementar filtrado por minLevel. 
        // Para el ejemplo, simplemente devolvemos un provider sin filtrar.
        builder.AddProvider(new XUnitLoggerProvider(outputHelper));
        return builder;
    }
}