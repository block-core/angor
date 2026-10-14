using System.Net;
using System.Text;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Angor.Sdk.Tests.Funding.Founder;

/// <summary>
/// Drives the real <see cref="MempoolIndexerAngorApi"/> over a fake HTTP handler to prove that an indexer outage
/// reaches callers as a failed Result and never as an empty success ("no investments").
/// </summary>
public class GetProjectInvestmentsIndexerFailureTests
{
    private const string ProjectIdValue = "angor1qtestproject";

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            HttpResponseMessage response = respond(request);
            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }

    private static GetProjectInvestments.GetProjectInvestmentsHandler CreateHandler(StubHandler httpHandler)
    {
        var client = new HttpClient(httpHandler);
        var clientFactory = new Mock<IHttpClientFactory>();
        clientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        var networkService = new Mock<INetworkService>();
        networkService.Setup(n => n.GetPrimaryIndexer())
            .Returns(new SettingsUrl { Url = "https://indexer.example", IsPrimary = true });

        var derivation = new Mock<IDerivationOperations>();
        derivation.Setup(d => d.ConvertAngorKeyToBitcoinAddress(It.IsAny<string>())).Returns("tb1qprojectaddress");

        var indexer = new MempoolIndexerAngorApi(
            NullLogger<MempoolIndexerAngorApi>.Instance,
            networkService.Object,
            derivation.Object,
            new MempoolIndexerMappers(NullLogger<MempoolIndexerMappers>.Instance),
            clientFactory.Object);

        var projectService = new Mock<IProjectService>();
        projectService.Setup(p => p.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success(new Project { Id = new ProjectId(ProjectIdValue), NostrPubKey = "npub" }));

        var handshakes = new Mock<IInvestmentHandshakeService>();
        handshakes.Setup(h => h.SyncHandshakesFromNostrAsync(It.IsAny<WalletId>(), It.IsAny<ProjectId>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Success<IEnumerable<InvestmentHandshake>>(Array.Empty<InvestmentHandshake>()));
        handshakes.Setup(h => h.GetHandshakesAsync(It.IsAny<WalletId>(), It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success<IEnumerable<InvestmentHandshake>>(Array.Empty<InvestmentHandshake>()));

        return new GetProjectInvestments.GetProjectInvestmentsHandler(
            indexer,
            Mock.Of<IIndexerService>(),
            projectService.Object,
            Mock.Of<INetworkConfiguration>(),
            handshakes.Object);
    }

    private static GetProjectInvestments.GetProjectInvestmentsRequest Request() =>
        new(new WalletId("wallet-1"), new ProjectId(ProjectIdValue));

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Handle_WhenIndexerReturnsServerError_ReturnsFailureNotEmptySuccess(HttpStatusCode status)
    {
        // Arrange
        var http = new StubHandler(_ => new HttpResponseMessage(status));
        var sut = CreateHandler(http);

        // Act
        var result = await sut.Handle(Request(), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue("an outage must not look like 'no investments'");
        result.Error.Should().NotBeNullOrWhiteSpace();
        http.Calls.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Handle_WhenIndexerReturnsHtmlWith200_ReturnsFailureNotEmptySuccess()
    {
        // Arrange - e.g. a captive portal or proxy error page served with 200 OK
        var http = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html><body>Bad gateway</body></html>", Encoding.UTF8, "text/html")
        });
        var sut = CreateHandler(http);

        // Act
        var result = await sut.Handle(Request(), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue("an unparseable body must not be treated as 'no investments'");
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_WhenIndexerRequestThrows_ReturnsFailure()
    {
        // Arrange
        var http = new StubHandler(_ => throw new HttpRequestException("connection refused"));
        var sut = CreateHandler(http);

        // Act
        var result = await sut.Handle(Request(), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("connection refused");
    }

    [Fact]
    public async Task Handle_WhenIndexerReturnsNotFound_ReturnsEmptySuccess()
    {
        // Arrange - 404 is "no transactions for this address", a legitimate empty result.
        var http = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var sut = CreateHandler(http);

        // Act
        var result = await sut.Handle(Request(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Investments.Should().BeEmpty();
    }
}
