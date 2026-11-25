using System.Net;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Angor.Test.Services;

public class MempoolSpaceIndexerApiNotFoundTests
{
    private sealed class StubHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public int Calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(new HttpResponseMessage(status) { RequestMessage = request });
        }
    }

    private static (MempoolSpaceIndexerApi Api, Mock<INetworkService> Network, StubHandler Handler) Create(
        HttpStatusCode status)
    {
        var handler = new StubHandler(status);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler));

        var network = new Mock<INetworkService>();
        network.Setup(n => n.GetPrimaryIndexer())
            .Returns(new SettingsUrl { Url = "https://indexer.example", IsPrimary = true });

        var api = new MempoolSpaceIndexerApi(NullLogger<MempoolSpaceIndexerApi>.Instance, factory.Object,
            network.Object, Mock.Of<IDerivationOperations>());
        return (api, network, handler);
    }

    [Fact]
    public async Task GetTransactionInfoByIdAsync_WhenNotFound_ReturnsNullWithoutRetryOrNotification()
    {
        var (api, network, handler) = Create(HttpStatusCode.NotFound);

        var result = await api.GetTransactionInfoByIdAsync("abc");

        Assert.Null(result);
        Assert.Equal(1, handler.Calls);
        network.Verify(n => n.NotifyIndexerUnreachable(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetTransactionHexByIdAsync_WhenNotFound_ThrowsNotFoundWithoutNotification()
    {
        var (api, network, _) = Create(HttpStatusCode.NotFound);

        await Assert.ThrowsAsync<IndexerResourceNotFoundException>(() => api.GetTransactionHexByIdAsync("abc"));

        network.Verify(n => n.NotifyIndexerUnreachable(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task FetchAddressHistoryAsync_WhenServiceUnavailable_NotifiesUnreachable()
    {
        var (api, network, _) = Create(HttpStatusCode.ServiceUnavailable);

        await Assert.ThrowsAsync<InvalidOperationException>(() => api.FetchAddressHistoryAsync("addr"));

        network.Verify(n => n.NotifyIndexerUnreachable(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }
}