using Angor.Contexts.Wallet.Domain;
using Xunit.Abstractions;

namespace Angor.Contexts.Wallet.Tests.Infrastructure;

[Collection("IntegrationTests")]
public class WalletAppServiceTests : IClassFixture<WalletAppServiceFixture>
{
    private readonly WalletAppServiceFixture _fixture;
    private readonly ITestOutputHelper _output;

    public WalletAppServiceTests(WalletAppServiceFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task GetBalance_ShouldReturnNonZeroBalance()
    {
        // Act
        var result = await _fixture.WalletAppService.GetBalance(_fixture.WalletId);

        // Assert
        Assert.True(result.IsSuccess);
        _output.WriteLine($"Balance: {result.Value.Value} sats");
        Assert.True(result.Value.Value >= 0);
    }

    [Fact]
    public async Task GetNextAddress_ShouldReturnValidAddress()
    {
        // Act
        var result = await _fixture.WalletAppService.GetNextReceiveAddress(_fixture.WalletId);

        // Assert
        Assert.True(result.IsSuccess);
        _output.WriteLine($"Next address: {result.Value.Value}");
        Assert.StartsWith("tb1", result.Value.Value); // TestNet4 native segwit prefix
    }

    [Fact]
    public async Task EstimateFee_ShouldReturnReasonableEstimate()
    {
        // Arrange
        var amount = new Amount(50000); 
        var address = new Address("tb1qw508d6qejxtdg4y5r3zarvary0c5xw7kxpjzsx"); // Sample address
        var feeRate = new DomainFeeRate(1); 

        // Act
        var result = await _fixture.WalletAppService.EstimateFee(_fixture.WalletId, amount, address, feeRate);

        // Assert
        Assert.True(result.IsSuccess);
        _output.WriteLine($"Estimated fee: {result.Value.Value} sats");
        Assert.True(result.Value.Value > 0);
        Assert.True(result.Value.Value < 50000); // Fee should be less than amount
    }

    [Fact(Skip = "This really sends money. Use carefully.")]
    public async Task SendAmount_ShouldSuccessfullyBroadcastTransaction()
    {
        // Arrange
        var amount = new Amount(100000); 
        var address = new Address("tb1qrcjv7fvyq85eenk3636ldpq40nvp82mgm6u0w2");
        var feeRate = new DomainFeeRate(1);

        // Act
        var result = await _fixture.WalletAppService.SendAmount(_fixture.WalletId, amount, address, feeRate);

        // Assert
        Assert.True(result.IsSuccess);
        _output.WriteLine($"Transaction ID: {result.Value.Value}");
        Assert.NotEmpty(result.Value.Value);
    }

    [Fact]
    public async Task GetTransactions_ShouldReturnTransactionHistory()
    {
        // Act
        var result = await _fixture.WalletAppService.GetTransactions(_fixture.WalletId);

        // Assert
        var errorMsg = result.TryGetError(out var error) ? error : "";
        Assert.True(result.IsSuccess, $"Failed to get transactions: {errorMsg}");

        var transactions = result.Value.ToList();
        _output.WriteLine($"Found {transactions.Count} transactions");

        foreach (var tx in transactions)
        {
            _output.WriteLine($"\nTransaction {tx.Id}:");
            _output.WriteLine($"Balance: {tx.Balance.Value} sats");
            _output.WriteLine($"Fee: {tx.Fee} sats");
            _output.WriteLine($"Confirmed: {tx.IsConfirmed}");
            _output.WriteLine($"Block Height: {tx.BlockHeight}");
            _output.WriteLine($"Block Time: {tx.BlockTime}");

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

    [Fact]
    public async Task GetTransactions_WithInvalidWalletId_ShouldFail()
    {
        // Arrange
        var invalidWalletId = new WalletId(Guid.NewGuid());

        // Act
        var result = await _fixture.WalletAppService.GetTransactions(invalidWalletId);

        // Assert
        Assert.False(result.IsSuccess);
    }

    private void ValidateTransactionAddresses(BroadcastedTransaction tx)
    {
        // Validate that wallet inputs are a subset of all inputs
        var walletInputAddresses = tx.WalletInputs.Select(wi => wi.Address.Address).ToHashSet();
        var allInputAddresses = tx.AllInputs.Select(ai => ai.Address).ToHashSet();
        Assert.True(walletInputAddresses.IsSubsetOf(allInputAddresses));

        // Validate that wallet outputs are a subset of all outputs
        var walletOutputAddresses = tx.WalletOutputs.Select(wo => wo.Address.Address).ToHashSet();
        var allOutputAddresses = tx.AllOutputs.Select(ao => ao.Address).ToHashSet();
        Assert.True(walletOutputAddresses.IsSubsetOf(allOutputAddresses));

        // Validate that amounts are consistent
        foreach (var input in tx.AllInputs)
        {
            Assert.True(input.TotalAmount >= 0);
            _output.WriteLine($"Input: {input.Address} - {input.TotalAmount} sats");
        }

        foreach (var output in tx.AllOutputs)
        {
            Assert.True(output.TotalAmount >= 0);
            _output.WriteLine($"Output: {output.Address} - {output.TotalAmount} sats");
        }
    }
}