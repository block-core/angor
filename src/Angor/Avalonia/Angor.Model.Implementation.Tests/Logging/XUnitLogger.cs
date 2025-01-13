namespace Angor.Test.Suppa;

using System;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

public class XUnitLogger : ILogger
{
    private readonly ITestOutputHelper outputHelper;
    private readonly string categoryName;

    public XUnitLogger(ITestOutputHelper outputHelper, string categoryName)
    {
        this.outputHelper = outputHelper ?? 
                            throw new ArgumentNullException(nameof(outputHelper));
        this.categoryName = categoryName;
    }

    public bool IsEnabled(LogLevel logLevel) => true; // Ajusta si quieres filtrar.

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception exception,
        Func<TState, Exception, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        outputHelper.WriteLine($"{DateTime.Now:HH:mm:ss} [{logLevel}] {categoryName}: {message}");

        if (exception is not null)
        {
            outputHelper.WriteLine(exception.ToString());
        }
    }

    public IDisposable BeginScope<TState>(TState state) => default!;
}