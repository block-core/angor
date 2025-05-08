using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nostr.Client.Client;

namespace Angor.Test.Services
{
    public class TestSignService
    {
        private readonly SignService _signService;

        public TestSignService()
        {
            var mockNetworkConfiguration = new Mock<INetworkConfiguration>();
            var mockNetworkStorage = new Mock<INetworkStorage>();

            mockNetworkConfiguration.Setup(nc => nc.GetAngorKey()).Returns("dummyAngorKey");
            mockNetworkConfiguration.Setup(nc => nc.GetNetwork()).Returns(Networks.Bitcoin.Testnet);

            mockNetworkStorage.Setup(ns => ns.GetSettings()).Returns(new SettingsInfo
            {
                Relays = new List<SettingsUrl>
                {
                    new() { Name = "", Url = "wss://relay.angor.io", IsPrimary = true },
                    new() { Name = "", Url = "wss://relay2.angor.io", IsPrimary = true },
                },
            });

            var communicationFactory = new NostrCommunicationFactory(new NullLogger<NostrWebsocketClient>(), new NullLogger<NostrCommunicationFactory>()); 
            var networkService = new NetworkService(mockNetworkStorage.Object, new HttpClient { BaseAddress = new Uri("https://angor.io") }, new NullLogger<NetworkService>(), mockNetworkConfiguration.Object); 
            var subscriptionsHanding = new RelaySubscriptionsHandling(new NullLogger<RelaySubscriptionsHandling>(), communicationFactory, networkService); 

            _signService = new SignService(communicationFactory, networkService, subscriptionsHanding);
        }

        //[Fact] // uncomment to test
        public async Task TestLookupInvestmentRequestApprovals()
        {
            string nostrPubKey = "5a05cc7a38e3875ee3242e5f068304a36c9609c4c15f5baaf7d75e8fcdfe36c5"; // Replace with actual public key

            var tcs = new TaskCompletionSource<bool>();

            bool failed = false;
            _signService.LookupSignedReleaseSigs(nostrPubKey, item => 
            {
                if(item.NostrEvent.Tags.FindFirstTagValue("subject") != "Release transaction signatures")
                {
                    failed = true;
                    tcs.SetResult(false);
                }
            }, 
            () =>
            {
                tcs.SetResult(true);
            });

            await tcs.Task;

            Assert.False(failed);
        }
    }
}
