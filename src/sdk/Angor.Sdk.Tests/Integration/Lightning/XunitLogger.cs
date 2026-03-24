using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Angor.Sdk.Tests.Integration.Lightning;

/// <summary>
/// Simple ILogger adapter that writes to xUnit ITestOutputHelper.
/// </summary>
public class XunitLogger<T> : ILogger<T>
{
    private readonly ITestOutputHelper _output;
    private readonly string _categoryName;

    public XunitLogger(ITestOutputHelper output)
    {
        _output = output;
        _categoryName = typeof(T).Name;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        try
        {
            var message = formatter(state, exception);
            _output.WriteLine($"[{logLevel:G}] [{_categoryName}] {message}");
            if (exception != null)
            {
                _output.WriteLine($"  Exception: {exception.Message}");
            }
        }
        catch
        {
            // Ignore - test output may be disposed
        }
    }
}

