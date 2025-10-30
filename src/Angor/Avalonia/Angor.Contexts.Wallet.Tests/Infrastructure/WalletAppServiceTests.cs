using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;
using Angor.Contexts.Wallet.Infrastructure.Impl;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using Angor.Contexts.Wallet.Tests.Infrastructure.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Angor.Contexts.Wallet.Tests.Infrastructure;

public class WalletAppServiceTests(ITestOutputHelper output)
{
    private readonly WalletId walletId = WalletAppService.SingleWalletId;
    
    [Fact(Skip = "Skipping failing test: missing indexer configuration.")]
    public async Task GetBalance_ShouldReturnNonZeroBalance()
    {
        // Arrange
        var sut = CreateSut();
        
        // Act
        var result = await sut.GetBalance(walletId);

        // Assert
        Assert.True(result.IsSuccess);
        output.WriteLine($"Balance: {result.Value.Sats} sats");
        Assert.True(result.Value.Sats >= 0);
    }

    [Fact(Skip = "Skipping failing test: missing indexer configuration.")]
    public async Task GetNextAddress_ShouldReturnValidAddress()
    {
        // Act
        var sut = CreateSut();
        var result = await sut.GetNextReceiveAddress(walletId);

        // Assert
        Assert.True(result.IsSuccess);
        output.WriteLine($"Next address: {result.Value.Value}");
        Assert.StartsWith("tb1", result.Value.Value); // TestNet4 native segwit prefix
    }

    [Fact(Skip = "Skipping failing test: missing indexer configuration.")]
    public async Task EstimateFee_ShouldReturnReasonableEstimate()
    {
        // Arrange
        var sut = CreateSut();
        var amount = new Amount(50000); 
        var address = new Address("tb1qw508d6qejxtdg4y5r3zarvary0c5xw7kxpjzsx"); // Sample address
        var feeRate = new DomainFeeRate(1); 

        // Act
        var result = await sut.EstimateFeeAndSize(walletId, amount, address, feeRate);

        // Assert
        Assert.True(result.IsSuccess);
        output.WriteLine($"Estimated fee: {result.Value.Fee} sats");
        Assert.True(result.Value.Fee > 0);
        Assert.True(result.Value.Fee < 50000); // Fee should be less than amount
    }

    [Fact(Skip = "This really sends money. Use carefully.")]
    public async Task SendAmount_ShouldSuccessfullyBroadcastTransaction()
    {
        // Arrange
        var sut = CreateSut();
        var amount = new Amount(100000); 
        var address = new Address("tb1qrcjv7fvyq85eenk3636ldpq40nvp82mgm6u0w2");
        var feeRate = new DomainFeeRate(1);

        // Act
        var result = await sut.SendAmount(walletId, amount, address, feeRate);

        // Assert
        Assert.True(result.IsSuccess);
        output.WriteLine($"Transaction ID: {result.Value.Value}");
        Assert.NotEmpty(result.Value.Value);
    }

    [Fact(Skip = "Skipping failing test: missing indexer configuration.")]
    public async Task GetTransactions_ShouldReturnTransactionHistory()
    {
        // Act
        var sut = CreateSut();
        var result = await sut.GetTransactions(walletId);

        // Assert
        var errorMsg = result.TryGetError(out var error) ? error : "";
        Assert.True(result.IsSuccess, $"Failed to get transactions: {errorMsg}");

        var transactions = result.Value.ToList();
        output.WriteLine($"Found {transactions.Count} transactions");

        foreach (var tx in transactions)
        {
            output.WriteLine($"\nTransaction {tx.Id}:");
            output.WriteLine($"Balance: {tx.GetBalance().Sats} sats");
            output.WriteLine($"Fee: {tx.Fee} sats");
            output.WriteLine($"Confirmed: {tx.IsConfirmed}");
            output.WriteLine($"Block Height: {tx.BlockHeight}");
            output.WriteLine($"Block Time: {tx.BlockTime}");

            // Validate basic structure of the transaction
            Assert.NotNull(tx.Id);
            Assert.NotNull(tx.RawJson);
            Assert.True(tx.Fee >= 0);

            // If the transaction is confirmed, it should have a block height and time
            if (tx.IsConfirmed)
            {
                Assert.NotNull(tx.BlockHeight);
                Assert.NotNull(tx.BlockTime);
            }

            // Validate inputs and outputs
            ValidateTransactionAddresses(tx);
        }
    }

    [Fact(Skip = "Skipping failing test: missing indexer configuration.")]
    public async Task GetTransactions_WithInvalidWalletId_ShouldFail()
    {
        // Arrange
        var sut = CreateSut();
        var invalidWalletId = new WalletId(Guid.NewGuid());

        // Act
        var result = await sut.GetTransactions(invalidWalletId);

        // Assert
        Assert.False(result.IsSuccess);
    }

    private void ValidateTransactionAddresses(BroadcastedTransaction tx)
    {
        // Validate that wallet inputs are a subset of all inputs
        var walletInputAddresses = tx.WalletInputs.Select(wi => wi.Address).ToHashSet();
        var allInputAddresses = tx.AllInputs.Select(ai => ai.Address).ToHashSet();
        Assert.True(walletInputAddresses.IsSubsetOf(allInputAddresses));

        // Validate that wallet outputs are a subset of all outputs
        var walletOutputAddresses = tx.WalletOutputs.Select(wo => wo.Address).ToHashSet();
        var allOutputAddresses = tx.AllOutputs.Select(ao => ao.Address).ToHashSet();
        Assert.True(walletOutputAddresses.IsSubsetOf(allOutputAddresses));

        // Validate that amounts are consistent
        foreach (var input in tx.AllInputs)
        {
            Assert.True(input.Amount.Sats >= 0);
            output.WriteLine($"Input: {input.Address} - {input.Amount} sats");
        }

        foreach (var txOutput in tx.AllOutputs)
        {
            Assert.True(txOutput.Amount.Sats >= 0);
            output.WriteLine($"Output: {txOutput.Address} - {txOutput.Amount} sats");
        }
    }

    private IWalletAppService CreateSut()
    {
        var serviceCollection = new ServiceCollection();
        
        var walletSecurityContext = new TestSecurityContext();
        var sensitiveWalletDataProvider = new TestSensitiveWalletDataProvider(
            "print foil moment average quarter keep amateur shell tray roof acoustic where",
            ""
        );

        serviceCollection.AddSingleton<IWalletStore>(new WalletStore(new InMemoryStore()));
        serviceCollection.AddSingleton<IWalletSecurityContext>(walletSecurityContext);
        serviceCollection.AddSingleton<ISensitiveWalletDataProvider>(sensitiveWalletDataProvider);
        
        WalletContextServices.Register(serviceCollection, TestFactory.CreateLogger(output), BitcoinNetwork.Testnet);
        var serviceProvider = serviceCollection.BuildServiceProvider();
        return serviceProvider.GetService<IWalletAppService>()!;
    }
}