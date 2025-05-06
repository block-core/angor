using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Investor.Operations;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using Angor.Contexts.Funding.Tests.TestDoubles;
using Angor.Contexts.Wallet.Domain;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using Angor.Shared;
using Microsoft.Extensions.DependencyInjection;
using Nostr.Client.Utils;
using Serilog;
using Xunit.Abstractions;
using Amount = Angor.Contexts.Funding.Projects.Domain.Amount;

namespace Angor.Contexts.Funding.Tests;

public class InvestmentAppServiceTests(ITestOutputHelper output)
{
    [Fact]
    public void CreateService()
    {
        CreateSut();
    }
    
    [Fact]
    public async Task CreateInvestmentTransaction()
    {
        var sut = CreateSut();
        var projectId = new ProjectId("angor1qkmmqqktfhe79wxp20555cdp5gfardr4s26wr00");
        var result = await sut.CreateDraft(sourceWalletId: Guid.Empty, projectId, new Amount(12345));
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : string.Empty);
    }
    
    [Fact]
    public async Task RequestFounderSignatures()
    {
        var sut = CreateSut();
        var projectId = new ProjectId("angor1qkmmqqktfhe79wxp20555cdp5gfardr4s26wr00");
        var investorKey = "03138f2811a0b7589d1b04a34b561f55093965757b31b239b3b201d93825455838";
        var txHex = "010000000001010bd918149ebabf50f237b1a56468275694d8659ba4c027e918b2fdd2daee6ca10000000000ffffffff077b00000000000000160014b6f6005969be7c57182a7d294c3434427a368eb00000000000000000236a2103138f2811a0b7589d1b04a34b561f55093965757b31b239b3b201d938254558380e0c00000000000022512084f0525ebccdfbf25a16c17b80376dee43340abe3ccf16dd469516305f269ae70e0c0000000000002251205aae5213e93335978561dea413cf06d84e50ca3b1c4a2df0032a9f7c07e6bb280e0c000000000000225120b2f7dd8d0e803799cba4197e7c0888985f8ff69113cab0dc273953afab6ab4160e0c000000000000225120fba3b11bb17f310d7cc02b09e911119324e53ce630625297ef021938a3a861644dc933770000000016001441566be59b2062a8cc3eddb528eb027dbbcbb18302483045022100cf44e9d7d793fa549fedd4835e09461538e797245ee662322004b6f69fa2b7d202205269573bbfba88abe0a39893acf9dd074260556a92c55be56cf5fdaca153da1f0121021ffb80288cff7f0c0e8dc30a1658f1f6c54fd89d8d03226acac1e6f733d1a04800000000";
        var txId = "1b55a4706d7315ea32bfe9c004781f0dee1df3ba18fd7d4423ebad2399dcf96a";
        var investmentTransaction = new CreateInvestment.Draft(investorKey, txHex, txId, new Amount(0));
        var result = await sut.RequestInvestment(Guid.Empty, projectId, investmentTransaction);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : string.Empty);
    }

    [Fact]
    public async Task GetInvestments()
    {
        var sut = CreateSut();
        var projectId = new ProjectId("angor1qkmmqqktfhe79wxp20555cdp5gfardr4s26wr00");
        var result = await sut.GetInvestments(projectId);
        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value);
    }

    [Fact]
    public async Task GetPendingInvestments()
    {
        var sut = CreateSut();
        var result = await sut.GetPendingInvestments(Guid.Empty, new ProjectId("angor1qatlv9htzte8vtddgyxpgt78ruyzaj57n4l7k46"));
        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value);
    }

    private IInvestmentAppService CreateSut()
    {
        var serviceCollection = new ServiceCollection();

        var logger = new LoggerConfiguration().WriteTo.TestOutput(output).CreateLogger();
        FundingContextServices.Register(serviceCollection, logger);
        serviceCollection.AddSingleton<ISeedwordsProvider>(sp => new TestingSeedwordsProvider("oven suggest panda hip orange cheap kite focus cross never tornado forget", "", sp.GetRequiredService<IDerivationOperations>()));

        var serviceProvider = serviceCollection.BuildServiceProvider();
        var projectAppService = serviceProvider.GetService<IInvestmentAppService>();

        return projectAppService!;
    }
}