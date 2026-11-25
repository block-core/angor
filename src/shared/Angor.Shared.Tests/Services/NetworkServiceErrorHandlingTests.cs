using System.Net;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Angor.Test.Services;

public class NetworkServiceErrorHandlingTests
{
    private const string IndexerUrl = "https://indexer.example";

    private static (NetworkService Service, Mock<INetworkStorage> Storage, SettingsInfo Settings) Create()
    {
        var settings = new SettingsInfo
        {
            Indexers = new List<SettingsUrl>
            {
                new() { Url = IndexerUrl, IsPrimary = true, Status = UrlStatus.Online }
            }
        };

        var storage = new Mock<INetworkStorage>();
        storage.Setup(s => s.GetSettings()).Returns(settings);

        var service = new NetworkService(storage.Object, Mock.Of<IHttpClientFactory>(),
            NullLogger<NetworkService>.Instance, Mock.Of<INetworkConfiguration>());

        return (service, storage, settings);
    }

    private static HttpResponseMessage Response(HttpStatusCode status) => new(status)
    {
        RequestMessage = new HttpRequestMessage(HttpMethod.Get, $"{IndexerUrl}/api/v1/tx/abc")
    };

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("down");
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public void CheckAndHandleError_WhenOutageStatus_MarksIndexerOffline(HttpStatusCode status)
    {
        // Arrange
        var (service, storage, settings) = Create();

        // Act
        service.CheckAndHandleError(Response(status));

        // Assert
        Assert.Equal(UrlStatus.Offline, settings.Indexers.Single().Status);
        storage.Verify(s => s.SetSettings(settings), Times.Once);
    }

    [Fact]
    public void CheckAndHandleError_WhenNotFound_DoesNotMarkIndexerOffline()
    {
        // Arrange - a 404 for an unknown transaction or address is not an outage.
        var (service, storage, settings) = Create();

        // Act
        service.CheckAndHandleError(Response(HttpStatusCode.NotFound));

        // Assert
        Assert.Equal(UrlStatus.Online, settings.Indexers.Single().Status);
        storage.Verify(s => s.SetSettings(It.IsAny<SettingsInfo>()), Times.Never);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.BadRequest)]
    public void CheckAndHandleError_WhenSuccessOrClientError_DoesNotMarkIndexerOffline(HttpStatusCode status)
    {
        // Arrange
        var (service, storage, settings) = Create();

        // Act
        service.CheckAndHandleError(Response(status));

        // Assert
        Assert.Equal(UrlStatus.Online, settings.Indexers.Single().Status);
        storage.Verify(s => s.SetSettings(It.IsAny<SettingsInfo>()), Times.Never);
    }

    [Fact]
    public void CheckAndHandleError_WhenOutageFromUnknownHost_DoesNotChangeConfiguredIndexers()
    {
        // Arrange
        var (service, storage, settings) = Create();
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://other.example/api/v1/tx/abc")
        };

        // Act
        service.CheckAndHandleError(response);

        // Assert
        Assert.Equal(UrlStatus.Online, settings.Indexers.Single().Status);
        storage.Verify(s => s.SetSettings(It.IsAny<SettingsInfo>()), Times.Never);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, false)]
    [InlineData(HttpStatusCode.NotFound, false)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.Forbidden, true)]
    [InlineData(HttpStatusCode.RequestTimeout, true)]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    public void IsServerUnavailable_ClassifiesStatusCodes(HttpStatusCode status, bool expected)
    {
        Assert.Equal(expected, NetworkService.IsServerUnavailable(Response(status)));
    }

    [Fact]
    public void CheckAndHandleError_WhenSuccessAfterOffline_MarksIndexerOnline()
    {
        // Arrange
        var (service, storage, settings) = Create();
        service.CheckAndHandleError(Response(HttpStatusCode.ServiceUnavailable));
        Assert.Equal(UrlStatus.Offline, settings.Indexers.Single().Status);

        // Act
        service.CheckAndHandleError(Response(HttpStatusCode.OK));

        // Assert
        Assert.Equal(UrlStatus.Online, settings.Indexers.Single().Status);
    }

    [Fact]
    public async Task CheckAndHandleError_WhenSuccessAfterCheckServicesMarkedOffline_MarksIndexerOnline()
    {
        // Arrange - a success has already reconciled the host, then the Settings probe marks it Offline
        var settings = new SettingsInfo
        {
            Indexers = new List<SettingsUrl> { new() { Url = IndexerUrl, IsPrimary = true, Status = UrlStatus.Online } }
        };
        var storage = new Mock<INetworkStorage>();
        storage.Setup(s => s.GetSettings()).Returns(settings);
        storage.Setup(s => s.GetNetwork()).Returns("Angornet");
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(new ThrowingHandler()));
        var service = new NetworkService(storage.Object, factory.Object,
            NullLogger<NetworkService>.Instance, Mock.Of<INetworkConfiguration>());

        service.CheckAndHandleError(Response(HttpStatusCode.OK));
        await service.CheckServices(force: true);
        Assert.Equal(UrlStatus.Offline, settings.Indexers.Single().Status);

        // Act
        service.CheckAndHandleError(Response(HttpStatusCode.OK));

        // Assert
        Assert.Equal(UrlStatus.Online, settings.Indexers.Single().Status);
    }
}
