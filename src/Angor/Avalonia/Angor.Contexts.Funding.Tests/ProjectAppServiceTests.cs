using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Contexts.Funding.Shared;
using Angor.Contexts.Funding.Tests.TestDoubles;
using Angor.Shared;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Xunit.Abstractions;

namespace Angor.Contexts.Funding.Tests;

public class ProjectAppServiceTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Get_latest_projects()
    {
        var sut = CreateSut();
        var result = await sut.Latest();
        
        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value);
    }
    
    [Fact]
    public async Task Get_founder_projects()
    {
        var sut = CreateSut();
        
        var projectId = new ProjectId("angor1qkmmqqktfhe79wxp20555cdp5gfardr4s26wr00");
        var result = await sut.GetFounderProjects(Guid.NewGuid());
        
        Assert.NotEmpty(result.Value);
    }

    [Fact]
    public async Task Get_Project_Statistics()
    {
        var sut = CreateSut();
        var projectId = new ProjectId("angor1qkmmqqktfhe79wxp20555cdp5gfardr4s26wr00");

        var result = await sut.GetProjectStatistics(projectId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
    }

    private IProjectAppService CreateSut()
    {
        var serviceCollection = new ServiceCollection();

        var logger = new LoggerConfiguration().WriteTo.TestOutput(output).CreateLogger();
        FundingContextServices.Register(serviceCollection, logger);
        serviceCollection.AddSingleton<ISeedwordsProvider>(sp => new TestingSeedwordsProvider("oven suggest panda hip orange cheap kite focus cross never tornado forget", "", sp.GetRequiredService<IDerivationOperations>()));

        var serviceProvider = serviceCollection.BuildServiceProvider();
        var projectAppService = serviceProvider.GetService<IProjectAppService>();

        return projectAppService!;
    }
}