using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Wallet.Application;
using Angor.Shared;
using Angor.Shared.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Angor.Cli.Tests;

[Collection("Composition")]
public class CompositionTests
{
    private readonly IServiceProvider _provider;

    public CompositionTests(CompositionFixture fixture)
    {
        _provider = fixture.ServiceProvider;
    }

    [Fact]
    public void BuildServiceProvider_ResolvesCoreServices()
    {
        _provider.GetRequiredService<IWalletAppService>().Should().NotBeNull();
        _provider.GetRequiredService<IProjectAppService>().Should().NotBeNull();
        _provider.GetRequiredService<IFounderAppService>().Should().NotBeNull();
        _provider.GetRequiredService<IInvestmentAppService>().Should().NotBeNull();
        _provider.GetRequiredService<INetworkConfiguration>().Should().NotBeNull();
        _provider.GetRequiredService<INetworkService>().Should().NotBeNull();
    }
}
