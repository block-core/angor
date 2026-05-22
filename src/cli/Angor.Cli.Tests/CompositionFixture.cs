using Angor.Cli.Composition;

namespace Angor.Cli.Tests;

/// <summary>
/// Shared DI container fixture to avoid LiteDB file locking conflicts.
/// All tests that need the real CompositionRoot share this single instance
/// via the [Collection("Composition")] attribute.
/// </summary>
public class CompositionFixture : IDisposable
{
    public IServiceProvider ServiceProvider { get; }

    public CompositionFixture()
    {
        ServiceProvider = CompositionRoot.BuildServiceProvider(isMcpMode: false, profileName: "CliTests");
    }

    public void Dispose()
    {
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

[CollectionDefinition("Composition")]
public class CompositionCollection : ICollectionFixture<CompositionFixture>
{
}
