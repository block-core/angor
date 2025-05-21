using Angor.Contexts.Funding.Shared;
using Angor.Contexts.Funding.Tests.TestDoubles;
using Angor.Contexts.Integration.WalletFunding;
using Angor.Contexts.Wallet.Infrastructure.Impl;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using Angor.Shared.Networks;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NBitcoin;
using Nostr.Client.Client;
using Nostr.Client.Communicator;
using Nostr.Client.Keys;
using Serilog;
using Xunit.Abstractions;
using Key = NBitcoin.Key;

namespace Angor.Contexts.Funding.Tests;

public class SigningTests(ITestOutputHelper output)
{
    [Fact]
    public void Create_sign_service()
    {
        CreateSignService();
    }

    [Fact]
    public async Task Post_investment_should_return_ok()
    {
        // Arrange
        var sut = CreateSignService();
        var founderNostrPubKey = NostrPrivateKey.GenerateNew().DerivePublicKey().Hex;
        var founderPubKey = new Key().PubKey.ToHex();
        var keyIdenfier = new KeyIdentifier(WalletAppService.SingleWalletId.Value, founderPubKey);
            
        // Act
        var result = await sut.PostInvestmentRequest2(keyIdenfier, "TEST", founderNostrPubKey);
        
        // Assert
        Assert.True(result.IsSuccess);
    }

    private ISignService CreateSignService()
    {
        var logger1 = new LoggerConfiguration()
            .WriteTo.TestOutput(output)
            .CreateLogger();


        var walletSensitiveDateProvider = new TestSensitiveWalletDataProvider(
            "print foil moment average quarter keep amateur shell tray roof acoustic where",
            ""
        );
        
        var serviceCollection = new ServiceCollection();
        FundingContextServices.Register(serviceCollection, logger1);
        serviceCollection.AddSingleton<ISeedwordsProvider, SeedwordsProvider>();
        serviceCollection.AddSingleton<ISensitiveWalletDataProvider>(walletSensitiveDateProvider);
        var provider = serviceCollection.BuildServiceProvider();

        return provider.GetRequiredService<ISignService>();
    }
}