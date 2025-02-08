using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Angor.UI.Model.Implementation.Tests.Logging;

public class XUnitLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper outputHelper;

    public XUnitLoggerProvider(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper ?? 
                            throw new ArgumentNullException(nameof(outputHelper));
    }

    public ILogger CreateLogger(string categoryName)
        => new XUnitLogger(outputHelper, categoryName);

    public void Dispose() { }
}