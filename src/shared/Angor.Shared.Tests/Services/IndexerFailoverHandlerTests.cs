using System.Collections.Concurrent;
using System.Net;
using Angor.Shared.Models;
using Angor.Shared.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Angor.Shared.Tests.Services;

public class IndexerFailoverHandlerTests
{
    private const string Genesis = "000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f";
    private readonly Mock<INetworkStorage> _storage = new();
    private readonly Mock<INetworkConfiguration> _configuration = new();
    private readonly ConcurrentQueue<string> _requests = new();

    private HttpClient CreateClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
    {
        _storage.Setup(x => x.GetSettings()).Returns(new SettingsInfo
        {
            Indexers = new List<SettingsUrl>
            {
                new() { Url = "https://primary.test", IsPrimary = true },
                new() { Url = "https://backup.test" }
            }
        });
        _configuration.Setup(x => x.GetGenesisBlockHash()).Returns(Genesis);
        return new HttpClient(new IndexerFailoverHandler(_storage.Object, _configuration.Object,
            NullLogger<IndexerFailoverHandler>.Instance)
        {
            AttemptTimeout = TimeSpan.FromMilliseconds(100),
            InnerHandler = new StubHandler((request, token) =>
            {
                _requests.Enqueue(request.RequestUri!.ToString());
                return send(request, token);
            })
        });
    }

    private static HttpResponseMessage Response(HttpStatusCode status, string body = "[]")
    {
        return new HttpResponseMessage(status) { Content = new StringContent(body) };
    }

    [Theory]
    [InlineData(503, "offline")]
    [InlineData(530, "error code: 1033")]
    [InlineData(429, "slow down")]
    [InlineData(403, "forbidden")]
    [InlineData(200, "<html>proxy error</html>")]
    public async Task Read_WhenPrimaryFails_UsesVerifiedBackupAndRemembersIt(int status, string body)
    {
        using HttpClient client = CreateClient((request, _) => Task.FromResult(
            request.RequestUri!.Host == "primary.test" ? Response((HttpStatusCode)status, body)
            : Response(HttpStatusCode.OK, request.RequestUri.AbsolutePath.EndsWith("/0") ? Genesis : "[]")));

        using HttpResponseMessage first = await client.GetAsync("https://primary.test/api/v1/address/test/txs");
        using HttpResponseMessage second = await client.GetAsync("https://primary.test/api/v1/address/other/txs");

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        _requests.Count(x => x.Contains("primary.test")).Should().Be(1);
        _requests.Count(x => x.EndsWith("block-height/0")).Should().Be(1);
        _storage.Verify(x => x.SetSettings(It.IsAny<SettingsInfo>()), Times.Never);
    }

    [Fact]
    public async Task Read_WhenBackupIsWrongNetwork_DoesNotSendAddressToIt()
    {
        using HttpClient client = CreateClient((request, _) => Task.FromResult(
            request.RequestUri!.Host == "primary.test" ? Response(HttpStatusCode.ServiceUnavailable)
            : Response(HttpStatusCode.OK, new string('1', 64))));

        Func<Task> act = () => client.GetAsync("https://primary.test/api/v1/address/private-address/txs");
        await act.Should().ThrowAsync<HttpRequestException>();
        _requests.Should().NotContain(x => x.Contains("backup.test/api/v1/address"));
    }

    [Fact]
    public async Task Read_WhenPrimaryTimesOut_Recovers()
    {
        using HttpClient client = CreateClient(async (request, token) =>
        {
            if (request.RequestUri!.Host == "primary.test")
            {
                await Task.Delay(Timeout.Infinite, token);
            }
            return Response(HttpStatusCode.OK, request.RequestUri.AbsolutePath.EndsWith("/0") ? Genesis : "[]");
        });
        using HttpResponseMessage response = await client.GetAsync("https://primary.test/api/v1/address/test/txs");
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task Read_WhenCancelled_DoesNotTryBackup()
    {
        using var cancellation = new CancellationTokenSource();
        using HttpClient client = CreateClient(async (_, token) =>
        {
            cancellation.Cancel();
            await Task.Delay(Timeout.Infinite, token);
            return Response(HttpStatusCode.OK);
        });
        Func<Task> act = () => client.GetAsync("https://primary.test/api/v1/address/test/txs", cancellation.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
        _requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task Read_WhenNotFound_DoesNotTreatMissingTransactionAsOutage()
    {
        using HttpClient client = CreateClient((_, _) => Task.FromResult(Response(HttpStatusCode.NotFound)));
        using HttpResponseMessage response = await client.GetAsync("https://primary.test/api/v1/tx/missing");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task Broadcast_WhenPrimaryFails_IsNotReplayed()
    {
        using HttpClient client = CreateClient((_, _) => Task.FromResult(Response(HttpStatusCode.ServiceUnavailable)));
        using HttpResponseMessage response = await client.PostAsync("https://primary.test/api/v1/tx", new StringContent("tx"));
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        _requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task Read_WhenAllServersFail_ReportsFailureAndAvoidsRepeatedProbes()
    {
        using HttpClient client = CreateClient((_, _) => Task.FromResult(Response(HttpStatusCode.ServiceUnavailable)));
        Func<Task> act = () => client.GetAsync("https://primary.test/api/v1/address/test/txs");
        await act.Should().ThrowAsync<HttpRequestException>();
        await act.Should().ThrowAsync<HttpRequestException>();
        _requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task ConcurrentReads_WhenPrimaryFails_ProbeBackupOnce()
    {
        using HttpClient client = CreateClient(async (request, token) =>
        {
            await Task.Delay(5, token);
            return request.RequestUri!.Host == "primary.test" ? Response(HttpStatusCode.ServiceUnavailable)
                : Response(HttpStatusCode.OK, request.RequestUri.AbsolutePath.EndsWith("/0") ? Genesis : "[]");
        });
        HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(i => client.GetAsync($"https://primary.test/api/v1/address/{i}/txs")));
        foreach (HttpResponseMessage response in responses)
        {
            response.IsSuccessStatusCode.Should().BeTrue();
            response.Dispose();
        }
        _requests.Count(x => x.EndsWith("block-height/0")).Should().Be(1);
    }

    [Fact]
    public async Task Read_WhenFallingBack_DoesNotForwardCredentials()
    {
        using HttpClient client = CreateClient((request, _) =>
        {
            if (request.RequestUri!.Host == "backup.test")
            {
                request.Headers.Contains("Authorization").Should().BeFalse();
                request.Headers.Contains("Cookie").Should().BeFalse();
                request.Headers.Contains("X-Api-Key").Should().BeFalse();
            }
            return Task.FromResult(request.RequestUri.Host == "primary.test"
                ? Response(HttpStatusCode.ServiceUnavailable)
                : Response(HttpStatusCode.OK, request.RequestUri.AbsolutePath.EndsWith("/0") ? Genesis : "[]"));
        });
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer private");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", "session=private");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", "private");

        using HttpResponseMessage response = await client.GetAsync("https://primary.test/api/v1/address/test/txs");
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task Read_WhenNetworkChanges_DoesNotReusePreviouslyVerifiedBackup()
    {
        using HttpClient client = CreateClient((request, _) => Task.FromResult(
            request.RequestUri!.Host == "primary.test" ? Response(HttpStatusCode.ServiceUnavailable)
            : Response(HttpStatusCode.OK, request.RequestUri.AbsolutePath.EndsWith("/0") ? Genesis : "[]")));
        using HttpResponseMessage first = await client.GetAsync("https://primary.test/api/v1/address/main/txs");
        _configuration.Setup(x => x.GetGenesisBlockHash()).Returns(new string('1', 64));

        Func<Task> act = () => client.GetAsync("https://primary.test/api/v1/address/testnet/txs");
        await act.Should().ThrowAsync<HttpRequestException>();
        _requests.Should().NotContain(x => x.Contains("backup.test/api/v1/address/testnet"));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            return send(request, token);
        }
    }
}
