using Angor.Data.Documents.Interfaces;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Funding.Services;
using CSharpFunctionalExtensions;
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

    // Deliberate behaviour change: one project whose on-chain validation fails (e.g. its indexer request
    // errors) is skipped; it no longer fails the whole discovery. Other discovered projects are still returned.
    [Fact]
    public async Task Discovery_WhenOneProjectValidationFails_SkipsItAndReturnsTheOthers()
    {
        // Arrange
        var relay = new Mock<IRelayService>();
        relay.Setup(x => x.LookupLatestProjects<ProjectInfo>(
                It.IsAny<Action<EventInfo<ProjectInfo>>>(), It.IsAny<Action>(), 30))
            .Callback<Action<EventInfo<ProjectInfo>>, Action, int>((next, complete, _) =>
            {
                next(new EventInfo<ProjectInfo>("event-bad", new ProjectInfo { ProjectIdentifier = "bad" }));
                next(new EventInfo<ProjectInfo>("event-good", new ProjectInfo { ProjectIdentifier = "good" }));
                complete();
            });

        var indexer = new Mock<IAngorIndexerService>();
        indexer.Setup(x => x.GetProjectByIdAsync("bad")).ThrowsAsync(new HttpRequestException("offline"));
        indexer.Setup(x => x.GetProjectByIdAsync("good"))
            .ReturnsAsync(new ProjectIndexerData { ProjectIdentifier = "good", NostrEventId = "event-good" });

        var goodProject = new Project { Id = new ProjectId("good") };
        var collection = new Mock<IGenericDocumentCollection<Project>>();
        collection.Setup(x => x.FindByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(Result.Success<IEnumerable<Project>>(new[] { goodProject }));

        var service = new DocumentProjectService(collection.Object, relay.Object, indexer.Object,
            NullLogger<DocumentProjectService>.Instance);

        // Act
        var result = await service.LatestFromNostrAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Select(p => p.Id.Value).Should().BeEquivalentTo("good");
        collection.Verify(x => x.FindByIdsAsync(It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { "good" }))),
            Times.Once);
        collection.Verify(x => x.FindByIdsAsync(It.Is<IEnumerable<string>>(ids => ids.Contains("bad"))), Times.Never);
    }

    [Fact]
    public async Task Discovery_WhenEveryProjectValidationFails_ReturnsNoValidProjectsFailure()
    {
        // Arrange
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

        // Act
        var result = await service.LatestFromNostrAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No valid on-chain projects");
    }
}