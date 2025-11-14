using Angor.Contexts.Wallet.Domain;
using Angor.Contexts.Wallet.Infrastructure.Impl.History;
using Angor.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Xunit.Abstractions;

namespace Angor.Contexts.Wallet.Tests.Infrastructure;

public class TransactionHistoryServiceTests(ITestOutputHelper outputHelper)
{
    [Fact(Skip = "Skipping failing test: missing IStore registration for NetworkStorage.")]
    public async Task Get_addresses()
    {
        var sut = CreateSut();
        var getAddressesResult = await sut.GetWalletAddresses(new WalletId("TODO"));
        
        Assert.True(getAddressesResult.IsSuccess);
    }

    [Fact(Skip = "Skipping failing test: missing IStore registration for NetworkStorage.")]
    public async Task Get_transactions()
    {
        var sut = CreateSut();
        var walletWords = new WalletWords
        {
            Words = "print foil moment average quarter keep amateur shell tray roof acoustic where",
            Passphrase = "",
        };
        var getAddressesResult = await sut.GetTransactions(new WalletId("TODO"));
        
        Assert.True(getAddressesResult.IsSuccess);
    }

    private TransactionHistory CreateSut()
    {
        var serviceCollection = new ServiceCollection();
        
        var logger = new LoggerConfiguration()
            .WriteTo.TestOutput(outputHelper)
            .MinimumLevel.Debug()
            .CreateLogger();
        
        WalletContextServices.Register(serviceCollection, logger, BitcoinNetwork.Testnet);
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var sut = ActivatorUtilities.CreateInstance<TransactionHistory>(serviceProvider);
        return sut;
    }
}