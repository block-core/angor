using Angor.Projects.Infrastructure;
using Angor.Projects.Infrastructure.Interfaces;
using Angor.Shared;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Xunit.Abstractions;

namespace Angor.Projects.Tests.Infrastructure;

public class ProjectAppServiceTests(ITestOutputHelper output)
{
    [Fact]
    public async Task CreateService()
    {
        var sut = CreateSut();
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