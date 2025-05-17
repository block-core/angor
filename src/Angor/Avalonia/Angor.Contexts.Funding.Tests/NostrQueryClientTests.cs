using System.Reactive.Linq;
using Angor.Contests.CrossCutting;
using Microsoft.Extensions.Logging;
using Nostr.Client.Client;
using Nostr.Client.Communicator;
using Nostr.Client.Requests;
using Serilog;
using Xunit.Abstractions;

namespace Angor.Contexts.Funding.Tests;

public class NostrQueryClientTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Test_client()
    {
        await CreateSut(async queryClient =>
        {
            var nostrFilter = new NostrFilter()
            {
                Authors = new[] { "f357d1789f47e6f0c9303582d2d67e6737f5dc68b87400570b38a833fdc6574a" },
                P = new[]{"c57cce49441740454e54fee15131b9699d54d4928a5eaa0756214dc21961cf1b"},
                E = new[]{"6c71f1e03f0b965306f35904cab5f8c6326524e9958ad6ef54b8a093730bad84"},
            };
        
            var result = await queryClient.Query(nostrFilter, TimeSpan.FromSeconds(5)).ToList();
            Assert.NotEmpty(result);
        });
    }

    private async Task CreateSut(Func<NostrQueryClient, Task> func)
    {
        var relayUri = "wss://relay.angor.io";
        var serilog = new LoggerConfiguration().WriteTo.TestOutput(output).CreateLogger();
        var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog(serilog));
        var logger = loggerFactory.CreateLogger<NostrWebsocketClient>();

        using var nostrWebsocketCommunicator = new NostrWebsocketCommunicator(new Uri(relayUri));
        await nostrWebsocketCommunicator.Start();
        var client = new NostrQueryClient(new NostrWebsocketClient(nostrWebsocketCommunicator, logger));
        await func(client);
    }


    [Fact]
    public async Task Concurrency_test()
    {
        List<string> list = ["6049b8f37b0968ceaa98c9518cc83fcb87563ca2b515c756456ce484b66a4176", "e204597e68988c6d7687e71e6cc3e00d0721384b6289389f547a20f98c915f03", "5ed6cd1736eaac70fb59db919b9c7ca793c83849846176dd3c7da23aedf9f2a9", "a5d441b0303e7dd95a6c467796bace242a730f14cc926616bee5cd59282b8153", "15c3b19890f705a06037957d04b7e94a38bc09634722c9462f204f061ab8dfa4", "6421c1b4046eed24bc30bc9c5ef96e622c6952644ea1aa962a5651f7eb4a63b5", "f85c80bea7581f42c73ad4cad5d1070551b30cc1c28d8db9226069d79eb45cf9", "e4c18570d3414dda3c6290b2ffb305292539d3a610607fb4f05ca1c16c89b676", "ff6910b065712f845339545d34728968f69f478d93a56c011a10e7850104de9b", "a0e520a8dbdb669a50ff761c1255b66bca19011c2f128e5d0a8ea530d319c469", "afe78ad6d7064642ef5d77fa5fbf3096e78785a5319269cc3367c8a3ae864add", "c4ce6a2113a96afa757fbaf6172c2834c76d920e1df5cd3df8df7d570b1d7b0b", "bac4e7f99ed383afd174daf2b941a47426fca3a498ea4681c022da93762823b5", "7f0f594501e65d341c4857e24948f3a31da819c1e76de7a66934890ea7aec870", "19c2cb8e2c7a03fbe2fb28778c313facb046c30b79b835e368ccc7d805c9679f", "ebfb2e57b9c8ff9611340a25db3d0a84ad12cd84121c6d93e614aad8badcf8eb", "c20b4c54fd0e3d2f1a958467c6364e83171ac22f48e2085f6c2dc1b1fbc431ff"];
        
        await CreateSut(async queryClient =>
        {
            var result = await list.ToObservable().SelectMany(s => queryClient.Query(new NostrFilter()
            {
                Ids = [s],
            }, TimeSpan.FromSeconds(5)))
            .ToList();
        });
    }
}