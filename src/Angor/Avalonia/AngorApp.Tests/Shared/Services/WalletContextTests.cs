using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Application;
using Angor.Sdk.Wallet.Domain;
using AngorApp.Model.Contracts.Wallet;
using AngorApp.UI.Shared.Services;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;

namespace AngorApp.Tests.Shared.Services;

public class WalletContextTests
{
    [Fact]
    public async Task ImportWallet_should_use_next_available_name_for_network_kind()
    {
        var walletAppService = new Mock<IWalletAppService>();
        var walletProvider = new Mock<IWalletProvider>();
        var existingId = new WalletId("existing-1");
        var newId = new WalletId("new-1");

        walletAppService
            .Setup(x => x.GetMetadatas())
            .ReturnsAsync(Result.Success<IEnumerable<WalletMetadata>>(new[]
            {
                new WalletMetadata("Bitcoin Wallet 1", existingId)
            }));

        walletProvider
            .Setup(x => x.Get(existingId))
            .ReturnsAsync(Result.Success(CreateWallet(existingId, "Bitcoin Wallet 1")));

        walletProvider
            .Setup(x => x.Get(newId))
            .ReturnsAsync(Result.Success(CreateWallet(newId, "Bitcoin Wallet 2")));

        walletAppService
            .Setup(x => x.CreateWallet(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Maybe<string>>(),
                It.IsAny<string>(),
                It.IsAny<BitcoinNetwork>()))
            .ReturnsAsync(Result.Success(newId));

        var context = new WalletContext(walletAppService.Object, walletProvider.Object, () => BitcoinNetwork.Mainnet);

        await WaitUntil(() => context.Wallets.Count == 1);

        var result = await context.ImportWallet("seed", Maybe<string>.None, "key", BitcoinNetwork.Mainnet, NetworkKind.Bitcoin);

        result.IsSuccess.Should().BeTrue();
        walletAppService.Verify(x => x.CreateWallet(
            "Bitcoin Wallet 2",
            "seed",
            Maybe<string>.None,
            "key",
            BitcoinNetwork.Mainnet), Times.Once);
    }

    private static IWallet CreateWallet(WalletId id, string name)
    {
        var wallet = new Mock<IWallet>();
        wallet.SetupGet(x => x.Id).Returns(id);
        wallet.SetupGet(x => x.Name).Returns(name);
        return wallet.Object;
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 1000)
    {
        var start = DateTimeOffset.UtcNow;
        while (!condition())
        {
            if ((DateTimeOffset.UtcNow - start).TotalMilliseconds > timeoutMs)
            {
                throw new TimeoutException("Timed out waiting for condition.");
            }

            await Task.Delay(10);
        }
    }
}
