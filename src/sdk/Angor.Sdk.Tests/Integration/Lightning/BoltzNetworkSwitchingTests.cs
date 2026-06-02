using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Angor.Sdk.Common;
using Angor.Shared.Integration.Lightning;
using Angor.Shared.Integration.Lightning.Models;
using Angor.Shared.Networks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Angor.Sdk.Tests.Integration.Lightning;

/// <summary>
/// Regression coverage for the bug where the lightning invoice generated on the
/// invoice payment page was always anchored to the Boltz signet (Angornet) endpoint
/// even after the user switched to mainnet in Settings.
///
/// Root cause was that <see cref="BoltzConfiguration.BaseUrl"/> was captured once at
/// app startup and the singleton <see cref="BoltzSwapService"/> set
/// <c>HttpClient.BaseAddress</c> in its constructor, so a later
/// <c>INetworkConfiguration.SetNetwork(...)</c> never reached the Lightning swap path.
/// </summary>
public class BoltzNetworkSwitchingTests
{
    private const string ExpectedMainnetHost = "api.boltz.exchange";
    private const string ExpectedTestnetHost = "test.boltz.angor.io";

    [Fact]
    public void ResolveBaseUrl_OnMainnet_ReturnsMainnetUrl()
    {
        var networkConfiguration = new NetworkConfiguration();
        networkConfiguration.SetNetwork(new BitcoinMain());
        var config = new BoltzConfiguration();

        var resolved = config.ResolveBaseUrl(networkConfiguration);

        Assert.Equal(ExpectedMainnetHost, new Uri(resolved).Host);
    }

    [Fact]
    public void ResolveBaseUrl_OnAngornet_ReturnsTestnetUrl()
    {
        var networkConfiguration = new NetworkConfiguration();
        networkConfiguration.SetNetwork(new Angornet());
        var config = new BoltzConfiguration();

        var resolved = config.ResolveBaseUrl(networkConfiguration);

        Assert.Equal(ExpectedTestnetHost, new Uri(resolved).Host);
    }

    [Fact]
    public void ResolveBaseUrl_WithOverride_AlwaysReturnsOverride()
    {
        var networkConfiguration = new NetworkConfiguration();
        networkConfiguration.SetNetwork(new BitcoinMain());
        var config = new BoltzConfiguration { OverrideBaseUrl = "https://my-boltz.example/" };

        var resolved = config.ResolveBaseUrl(networkConfiguration);

        Assert.Equal("my-boltz.example", new Uri(resolved).Host);
    }

    /// <summary>
    /// The original bug: switching to mainnet at runtime still produced a signet
    /// Lightning invoice because the Boltz HTTP base URL was frozen at startup.
    /// This test creates a single <see cref="BoltzSwapService"/> instance (as DI
    /// does — singleton lifetime), fires the swap-creation HTTP request twice
    /// with a runtime network switch in between, and asserts that the second
    /// request hits the mainnet host.
    /// </summary>
    [Fact]
    public async Task BoltzSwapService_FollowsRuntimeNetworkSwitch_FromAngornetToMainnet()
    {
        var networkConfiguration = new NetworkConfiguration();
        networkConfiguration.SetNetwork(new Angornet());

        var handler = new RecordingHandler();
        var httpClient = new HttpClient(handler);
        var config = new BoltzConfiguration { UseV2Prefix = true };

        var service = new BoltzSwapService(
            httpClient,
            config,
            networkConfiguration,
            new NullLogger<BoltzSwapService>());

        // First call: still on Angornet (signet). The bug would have pinned the host here.
        await service.GetReverseSwapFeesAsync();
        var firstHost = handler.LastRequestUri?.Host;

        // Simulate the Settings → Switch network → Mainnet action.
        networkConfiguration.SetNetwork(new BitcoinMain());

        // Second call: must now hit the mainnet host on the SAME singleton instance.
        await service.GetReverseSwapFeesAsync();
        var secondHost = handler.LastRequestUri?.Host;

        Assert.Equal(ExpectedTestnetHost, firstHost);
        Assert.Equal(ExpectedMainnetHost, secondHost);
    }

    [Fact]
    public async Task BoltzSwapService_FollowsRuntimeNetworkSwitch_FromMainnetToAngornet()
    {
        var networkConfiguration = new NetworkConfiguration();
        networkConfiguration.SetNetwork(new BitcoinMain());

        var handler = new RecordingHandler();
        var httpClient = new HttpClient(handler);
        var config = new BoltzConfiguration { UseV2Prefix = true };

        var service = new BoltzSwapService(
            httpClient,
            config,
            networkConfiguration,
            new NullLogger<BoltzSwapService>());

        await service.GetReverseSwapFeesAsync();
        var firstHost = handler.LastRequestUri?.Host;

        networkConfiguration.SetNetwork(new Angornet());

        await service.GetReverseSwapFeesAsync();
        var secondHost = handler.LastRequestUri?.Host;

        Assert.Equal(ExpectedMainnetHost, firstHost);
        Assert.Equal(ExpectedTestnetHost, secondHost);
    }

    [Fact]
    public async Task BoltzSwapService_RequestPath_IncludesV2Prefix()
    {
        var networkConfiguration = new NetworkConfiguration();
        networkConfiguration.SetNetwork(new BitcoinMain());

        var handler = new RecordingHandler();
        var httpClient = new HttpClient(handler);
        var config = new BoltzConfiguration { UseV2Prefix = true };

        var service = new BoltzSwapService(
            httpClient,
            config,
            networkConfiguration,
            new NullLogger<BoltzSwapService>());

        await service.GetReverseSwapFeesAsync();

        Assert.NotNull(handler.LastRequestUri);
        Assert.Equal("/v2/swap/reverse", handler.LastRequestUri!.AbsolutePath);
    }

    /// <summary>
    /// Captures the URI of every outbound request and returns a minimal valid
    /// reverse-swap-info JSON body so the calling code does not fail before we
    /// can inspect what URL it built.
    /// </summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;

            // Shape required by ReverseSwapInfoResponse so deserialisation succeeds.
            const string body = """
                {
                  "BTC": {
                    "BTC": {
                      "fees": { "percentage": 0.5, "minerFees": { "claim": 100 } },
                      "limits": { "minimal": 10000, "maximal": 25000000 }
                    }
                  }
                }
                """;

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
                }
            };
            return Task.FromResult(response);
        }
    }
}
