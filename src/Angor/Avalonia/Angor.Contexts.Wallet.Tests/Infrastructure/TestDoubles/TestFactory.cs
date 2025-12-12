using Serilog;
using Xunit.Abstractions;

namespace Angor.Sdk.Wallet.Tests.Infrastructure;

public static class TestFactory
{
    public static ILogger CreateLogger(ITestOutputHelper outputHelper)
    {
        return new LoggerConfiguration()
            .WriteTo.TestOutput(outputHelper)
            .MinimumLevel.Debug()
            .CreateLogger();
    }
}