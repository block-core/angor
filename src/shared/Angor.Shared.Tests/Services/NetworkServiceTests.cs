using System.Net;
using System.Text.Json;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Angor.Test.Services;

public class NetworkServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CheckServices_WhenSettingsChangeDuringRequest_PreservesCurrentNetworkAndIndexer(bool switchNetwork)
    {
        string network = "Main";
        SettingsInfo stored = new()
        {
            Indexers = new List<SettingsUrl>
            {
                new() { Url = "https://old.example", IsPrimary = true }
            }
        };
        Mock<INetworkStorage> storage = new();
        storage.Setup(value => value.GetNetwork()).Returns(() => network);
        // Local storage returns snapshots, not a shared in-memory object.
        storage.Setup(value => value.GetSettings()).Returns(() =>
            JsonSerializer.Deserialize<SettingsInfo>(JsonSerializer.Serialize(stored))!);
        storage.Setup(value => value.SetSettings(It.IsAny<SettingsInfo>()))
            .Callback<SettingsInfo>(value => stored = value);
        DelayedHandler handler = new();
        using HttpClient client = new(handler);
        Mock<IHttpClientFactory> factory = new();
        factory.Setup(value => value.CreateClient(It.IsAny<string>())).Returns(client);
        NetworkService service = new(storage.Object, factory.Object,
            NullLogger<NetworkService>.Instance, Mock.Of<INetworkConfiguration>());

        Task checking = service.CheckServices(true);
        await handler.Started.Task;
        network = switchNetwork ? "Angornet" : "Main";
        stored = new SettingsInfo
        {
            Indexers = new List<SettingsUrl>
            {
                new() { Url = "https://selected.example", IsPrimary = true },
                new() { Url = "https://old.example", IsPrimary = false }
            }
        };
        handler.Response.SetResult(new HttpResponseMessage(HttpStatusCode.OK));
        await checking;

        Assert.Equal(switchNetwork ? "Angornet" : "Main", network);
        Assert.Equal("https://selected.example", service.GetPrimaryIndexer().Url);
        Assert.Equal(2, stored.Indexers.Count);
        if (switchNetwork)
            storage.Verify(value => value.SetSettings(It.IsAny<SettingsInfo>()), Times.Never);
        else
            Assert.Equal(UrlStatus.Online, stored.Indexers[1].Status);
    }

    private sealed class DelayedHandler : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<HttpResponseMessage> Response { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            return Response.Task;
        }
    }
}
