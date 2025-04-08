using Angor.Contexts.Funding;
using Angor.Contexts.Funding.Projects.Infrastructure;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Contexts.Funding.Tests.TestDoubles;
using Angor.Shared;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Xunit.Abstractions;

namespace Angor.Contexts.Funding.Tests;

public class ProjectAppServiceTests(ITestOutputHelper output)
{
    [Fact]
    public async Task GetProjects()
    {
        var sut = CreateSut();
        var result = await sut.Latest();
        Assert.NotEmpty(result);
    }

    private IProjectAppService CreateSut()
    {
        var serviceCollection = new ServiceCollection();

        var logger = new LoggerConfiguration().WriteTo.TestOutput(output).CreateLogger();
        FundingContext.Register(serviceCollection, logger);
        serviceCollection.AddSingleton<ISeedwordsProvider>(sp => new TestingSeedwordsProvider("print foil moment average quarter keep amateur shell tray roof acoustic where", "", sp.GetRequiredService<IDerivationOperations>()));

        var serviceProvider = serviceCollection.BuildServiceProvider();
        var projectAppService = serviceProvider.GetService<IProjectAppService>();

        return projectAppService!;
    }
}