using Angor.Projects.Domain;
using Angor.Projects.Infrastructure;
using Angor.Projects.Infrastructure.Interfaces;
using Angor.Shared;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Xunit.Abstractions;

namespace Angor.Projects.Tests;

public class ProjectAppServiceTests(ITestOutputHelper output)
{
    [Fact]
    public void CreateService()
    {
        CreateSut();
    }
    
    [Fact]
    public async Task GetProjects()
    {
        var sut = CreateSut();
        var result = await sut.Latest();
        Assert.NotEmpty(result);
    }
    
    [Fact]
    public async Task Invest()
    {
        var sut = CreateSut();
        var projectId = new ProjectId("angor1qptj5qunu2mnwmfcspqc5pxlfscazcqlswt7d74");
        var result = await sut.Invest(Guid.Empty, projectId, new Amount(10000));
        
        Assert.True(result.IsSuccess);
    }

    private IProjectAppService CreateSut()
    {
        var serviceCollection = new ServiceCollection();
        
        var logger = new LoggerConfiguration().WriteTo.TestOutput(output).CreateLogger();
        ProjectServices.Register(serviceCollection, logger);
        serviceCollection.AddSingleton<IInvestorKeyProvider>(sp => new TestingInvestorKeyProvider("print foil moment average quarter keep amateur shell tray roof acoustic where", "", sp.GetRequiredService<IDerivationOperations>()));
        
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var projectAppService = serviceProvider.GetService<IProjectAppService>();
        
        return projectAppService!;
    }
}