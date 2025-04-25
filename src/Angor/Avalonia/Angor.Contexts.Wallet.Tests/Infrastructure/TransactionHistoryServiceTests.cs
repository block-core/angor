using Angor.Contexts.Wallet.Domain;
using Angor.Contexts.Wallet.Infrastructure.History;
using Angor.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Xunit.Abstractions;

namespace Angor.Contexts.Wallet.Tests.Infrastructure;

public class TransactionHistoryServiceTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public async Task Get_addresses()
    {
        var sut = CreateSut();
        var getAddressesResult = await sut.GetWalletAddresses(new WalletWords()
        {
            Words = "print foil moment average quarter keep amateur shell tray roof acoustic where",
            Passphrase = "",
        });
        
        Assert.True(getAddressesResult.IsSuccess);
    }
    
    [Fact]
    public async Task Get_transactionIds()
    {
        var sut = CreateSut();
        var getAddressesResult = await sut.GetTransactions("tb1qp8mux3kkqzxys60eu8r867kyvf9p67vuj6mkl2");
        
        Assert.True(getAddressesResult.IsSuccess);
    }
    
    [Fact]
    public async Task Get_transactions()
    {
        var sut = CreateSut();
        var walletWords = new WalletWords
        {
            Words = "print foil moment average quarter keep amateur shell tray roof acoustic where",
            Passphrase = "",
        };
        var getAddressesResult = await sut.GetTransactions(walletWords);
        
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