using System.Net;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Angor.Test.Services;

public class MempoolSpaceIndexerApiPublishTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                RequestMessage = request,
                Content = new StringContent(body)
            });
        }
    }

    [Fact]
    public async Task PublishTransactionAsync_WithMaxAttempts1_MakesSingleCallOnMissingInputs()
    {
        // Arrange
        var handler = new StubHandler(HttpStatusCode.BadRequest, "bad-txns-inputs-missingorspent");
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler));
        var network = new Mock<INetworkService>();
        network.Setup(n => n.GetPrimaryIndexer())
            .Returns(new SettingsUrl { Url = "https://indexer.example", IsPrimary = true });
        var api = new MempoolSpaceIndexerApi(NullLogger<MempoolSpaceIndexerApi>.Instance, factory.Object,
            network.Object, Mock.Of<IDerivationOperations>());

        // Act
        var error = await api.PublishTransactionAsync("00", maxAttempts: 1);

        // Assert
        Assert.Contains("missingorspent", error);
        Assert.Equal(1, handler.Calls);
    }
}