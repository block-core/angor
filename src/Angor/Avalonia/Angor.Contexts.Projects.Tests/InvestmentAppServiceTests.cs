using Angor.Contexts.Funding;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Investor.CreateInvestment;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Projects.Tests.TestDoubles;
using Angor.Shared;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Xunit.Abstractions;

namespace Angor.Contexts.Projects.Tests;

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
        var projectId = new ProjectId("angor1qzd42rmpz3ha94zutfdaurdfxvfquj8zlt73rt2");
        var result = await sut.CreateInvestmentTransaction(walletId: Guid.Empty, projectId, new Amount(12345));
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : string.Empty);
        //Assert.NotEmpty(result.Value);
    }
    
    [Fact]
    public async Task RequestFounderSignatures()
    {
        var sut = CreateSut();
        var projectId = new ProjectId("angor1qzd42rmpz3ha94zutfdaurdfxvfquj8zlt73rt2");
        var investorKey = "024c19a97a357bc22094b0554a74fbef686793bae5da01a17611636a7019b58967";
        var txHex = "010000000001010bd918149ebabf50f237b1a56468275694d8659ba4c027e918b2fdd2daee6ca10000000000ffffffff067b00000000000000160014136aa1ec228dfa5a8b8b4b7bc1b5266241c91c5f0000000000000000236a21024c19a97a357bc22094b0554a74fbef686793bae5da01a17611636a7019b58967d2040000000000002251203e0df1630271629682bc273e8b975f7bfd768688527f8d309338c2909072f7a9780e000000000000225120d4a734b06f80cef2bd068158b3c55f14f12200b94894d8d2ec4213fd377b6053ef1c0000000000002251200125f144a31c225dee569feec4c0e4113c24bfff18b26f2f206d777c877ef058faca33770000000016001441566be59b2062a8cc3eddb528eb027dbbcbb18302483045022100a28bbc7b4323eb133282227d85afe4f3ec9a8237479fbe252e67a7d5769163710220402ad16e9a02825e7b6b9bed3d75c63b4e95bbb8eff12f6c250c071c3ae3eaa60121021ffb80288cff7f0c0e8dc30a1658f1f6c54fd89d8d03226acac1e6f733d1a04800000000";
        var txId = "77e79c8c207190e66c356f11965c96681eefcb367597f0a926ee0a434fdf78c6";
        var investmentTransaction = new InvestmentTransaction(investorKey, txHex, txId);
        var result = await sut.RequestFounderSignatures(projectId, investmentTransaction);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : string.Empty);
    }

    [Fact]
    public async Task GetInvestments()
    {
        var sut = CreateSut();
        var projectId = new ProjectId("angor1qptj5qunu2mnwmfcspqc5pxlfscazcqlswt7d74");
        var result = await sut.GetInvestments(projectId);
        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value);
    }

    private IInvestmentAppService CreateSut()
    {
        var serviceCollection = new ServiceCollection();

        var logger = new LoggerConfiguration().WriteTo.TestOutput(output).CreateLogger();
        FundingContext.Register(serviceCollection, logger);
        serviceCollection.AddSingleton<ISeedwordsProvider>(sp => new TestingSeedwordsProvider("print foil moment average quarter keep amateur shell tray roof acoustic where", "", sp.GetRequiredService<IDerivationOperations>()));

        var serviceProvider = serviceCollection.BuildServiceProvider();
        var projectAppService = serviceProvider.GetService<IInvestmentAppService>();

        return projectAppService!;
    }
}