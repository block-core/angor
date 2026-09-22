using Angor.Data.Documents.Interfaces;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Shared.Models;
using Angor.Shared.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Angor.Sdk.Tests.Funding.Projects;

public class ProjectDiscoveryRecoveryTests
{
    [Fact]
    public async Task Discovery_WhenFirstRelayCompletesBeforeConnectionsSettle_Retries()
    {
        var relay = new Mock<IRelayService>();
        int attempts = 0;
        relay.Setup(x => x.LookupLatestProjects<ProjectInfo>(
                It.IsAny<Action<EventInfo<ProjectInfo>>>(), It.IsAny<Action>(), 30))
            .Callback<Action<EventInfo<ProjectInfo>>, Action, int>((next, complete, _) =>
            {
                if (++attempts == 2)
                    next(new EventInfo<ProjectInfo>("event", new ProjectInfo { ProjectIdentifier = "project" }));
                complete();
            });
        var service = new DocumentProjectService(Mock.Of<IGenericDocumentCollection<Project>>(),
            relay.Object, Mock.Of<IAngorIndexerService>(), NullLogger<DocumentProjectService>.Instance);

        var result = await service.LatestFromNostrAsync();

        attempts.Should().Be(2);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("on-chain"); // Discovery succeeded; the fake project has no transaction.
    }

    [Fact]
    public async Task Discovery_WhenValidationServerFails_ReportsOutageInsteadOfNoProjects()
    {
        var relay = new Mock<IRelayService>();
        relay.Setup(x => x.LookupLatestProjects<ProjectInfo>(
                It.IsAny<Action<EventInfo<ProjectInfo>>>(), It.IsAny<Action>(), 30))
            .Callback<Action<EventInfo<ProjectInfo>>, Action, int>((next, complete, _) =>
            {
                next(new EventInfo<ProjectInfo>("event", new ProjectInfo { ProjectIdentifier = "project" }));
                complete();
            });
        var indexer = new Mock<IAngorIndexerService>();
        indexer.Setup(x => x.GetProjectByIdAsync("project")).ThrowsAsync(new HttpRequestException("offline"));
        var service = new DocumentProjectService(Mock.Of<IGenericDocumentCollection<Project>>(),
            relay.Object, indexer.Object, NullLogger<DocumentProjectService>.Instance);

        var result = await service.LatestFromNostrAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("servers are unavailable");
    }
}
