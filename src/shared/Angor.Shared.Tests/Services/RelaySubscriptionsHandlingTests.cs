using System.Reactive.Disposables;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nostr.Client.Client;

namespace Angor.Test.Services;

/// <summary>
/// Covers the timeout-based subscription cleanup added to close the gap described in
/// https://github.com/block-core/angor/issues/967: a subscription must be force-closed after a
/// fixed deadline even if EOSE never arrives from every relay (or arrives from none, e.g. because
/// no relays are configured/connected in these tests).
/// </summary>
public class RelaySubscriptionsHandlingTests
{
    private static (RelaySubscriptionsHandling handling, INetworkService networkService) CreateSut(TimeSpan timeout)
    {
        var mockNetworkConfiguration = new Mock<INetworkConfiguration>();
        var mockNetworkStorage = new Mock<INetworkStorage>();

        mockNetworkConfiguration.Setup(nc => nc.GetAngorKey()).Returns("dummyAngorKey");
        mockNetworkConfiguration.Setup(nc => nc.GetNetwork()).Returns(Networks.Bitcoin.Testnet);
        mockNetworkConfiguration.Setup(nc => nc.GetDiscoveryRelays()).Returns(new List<SettingsUrl>());

        // Deliberately no relays configured, so no real websocket connections are attempted -
        // these tests only need to exercise the local timeout-cleanup bookkeeping.
        mockNetworkStorage.Setup(ns => ns.GetSettings()).Returns(new SettingsInfo
        {
            Relays = new List<SettingsUrl>(),
        });

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new HttpClientHandler())
            {
                BaseAddress = new Uri("https://mempool.space/api/v1/")
            });

        var communicationFactory = new NostrCommunicationFactory(new NullLogger<NostrWebsocketClient>(), new NullLogger<NostrCommunicationFactory>());
        var networkService = new NetworkService(mockNetworkStorage.Object, mockHttpClientFactory.Object, new NullLogger<NetworkService>(), mockNetworkConfiguration.Object);

        var handling = new RelaySubscriptionsHandling(new NullLogger<RelaySubscriptionsHandling>(), communicationFactory, networkService, timeout);

        return (handling, networkService);
    }

    [Fact]
    public async Task Subscription_IsForceClosed_AfterTimeout_WhenEoseNeverArrives()
    {
        var (handling, _) = CreateSut(TimeSpan.FromMilliseconds(100));

        var added = handling.TryAddRelaySubscription("sub-key", Disposable.Empty);
        added.Should().BeTrue();
        handling.RelaySubscriptionAdded("sub-key").Should().BeTrue();

        // Give the timer time to fire and force-close the subscription.
        await WaitForConditionAsync(() => !handling.RelaySubscriptionAdded("sub-key"), TimeSpan.FromSeconds(2));

        handling.RelaySubscriptionAdded("sub-key").Should().BeFalse();
    }

    [Fact]
    public async Task PendingEoseAction_IsRemoved_WhenSubscriptionTimesOut()
    {
        var (handling, _) = CreateSut(TimeSpan.FromMilliseconds(100));

        var eoseActionInvoked = false;
        handling.TryAddRelaySubscription("sub-key", Disposable.Empty);
        handling.TryAddEoseAction("sub-key", () => eoseActionInvoked = true);

        await WaitForConditionAsync(() => !handling.RelaySubscriptionAdded("sub-key"), TimeSpan.FromSeconds(2));

        // The timeout path removes the pending action rather than invoking it - the caller's own
        // timeout (e.g. a 30s CTS in BuildRecoveryTransaction) is what surfaces the failure.
        eoseActionInvoked.Should().BeFalse();
    }

    [Fact]
    public async Task Subscription_IsNotForceClosed_WhenClosedManuallyBeforeTimeout()
    {
        var (handling, _) = CreateSut(TimeSpan.FromMilliseconds(300));

        handling.TryAddRelaySubscription("sub-key", Disposable.Empty);
        handling.CloseSubscription("sub-key");

        handling.RelaySubscriptionAdded("sub-key").Should().BeFalse();

        // Wait past the original timeout window to make sure no stray timer callback throws or
        // otherwise misbehaves after the manual close already cancelled it.
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        handling.RelaySubscriptionAdded("sub-key").Should().BeFalse();
    }

    [Fact]
    public async Task KeepActiveSubscription_IsNotForceClosed_ByTimeout()
    {
        var (handling, _) = CreateSut(TimeSpan.FromMilliseconds(100));

        handling.TryAddRelaySubscription("sub-key", Disposable.Empty, keepActive: true);

        // Wait well past the timeout window - a keepActive subscription must survive.
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        handling.RelaySubscriptionAdded("sub-key").Should().BeTrue();
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;

            await Task.Delay(25);
        }

        condition().Should().BeTrue("condition should have become true within the timeout");
    }
}
