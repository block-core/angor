using Angor.UI.Model.Implementation.Projects;
using Angor.UI.Model.Implementation.Tests.Logging;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Angor.UI.Model.Implementation.Tests;

public class ProjectServiceTests
{
    private readonly ILoggerFactory loggerFactory;

    public ProjectServiceTests(ITestOutputHelper output)
    {
        loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddXUnitLogger(output);
        });
    }

    [Fact]
    public async Task GetProjectsFromService()
    {
        var service = new ProjectService(DependencyFactory.GetIndexerService(loggerFactory), DependencyFactory.GetRelayService(loggerFactory));

        var projectsList = await service.Latest();

        Assert.NotEmpty(projectsList);
    }
}