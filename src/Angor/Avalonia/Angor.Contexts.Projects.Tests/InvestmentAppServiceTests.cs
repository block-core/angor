using Angor.Contexts.Funding;
using Angor.Contexts.Funding.Investor;
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
        var result = await sut.CreateInvestmentTransaction(Guid.Empty, projectId, new Amount(12345));
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : string.Empty);
        //Assert.NotEmpty(result.Value);
    }

    [Fact]
    public async Task GetInvestments()
    {
        var sut = CreateSut();
        var projectId = new ProjectId("angor1qzd42rmpz3ha94zutfdaurdfxvfquj8zlt73rt2");
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