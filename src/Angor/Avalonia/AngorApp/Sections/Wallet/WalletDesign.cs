using System.Threading.Tasks;
using AngorApp.Model;
using AngorApp.Sections.Browse;
using CSharpFunctionalExtensions;

namespace AngorApp.Sections.Wallet;

public class WalletDesign : IWallet
{
    public IEnumerable<IBroadcastedTransaction> History { get; } =
    [
        new BroadcastedTransactionDesign { Address = "someaddress1", Amount = 0.0001m, UtxoCount = 12, Path = "path", ViewRawJson = "json" },
        new BroadcastedTransactionDesign { Address = "someaddress2", Amount = 0.0003m, UtxoCount = 15, Path = "path", ViewRawJson = "json" },
        new BroadcastedTransactionDesign { Address = "someaddress3", Amount = 0.0042m, UtxoCount = 15, Path = "path", ViewRawJson = "json" },
        new BroadcastedTransactionDesign { Address = "someaddress4", Amount = 0.00581m, UtxoCount = 15, Path = "path", ViewRawJson = "json" }
    ];

    public decimal? Balance { get; set; } = 0.2m;

    public string ReceiveAddress { get; } = SampleData.TestNetBitcoinAddress;

    public async Task<Result<IUnsignedTransaction>> CreateTransaction(decimal amount, string address, decimal feerate)
    {
        await Task.Delay(1000);

        //return Result.Failure<ITransaction>("Transaction creation failed");
        return new UnsignedTransactionDesign
        {
            Address = address,
            Amount = amount,
            TotalFee = feerate * 0.00001m,
            FeeRate = feerate
        };
    }

    public Result IsAddressValid(string address)
    {
        var value = BitcoinAddressValidator.ValidateBitcoinAddress(address, Network);
        return value.IsValid ? Result.Success() : Result.Failure(value.Message);
    }

    public BitcoinNetwork Network => BitcoinNetwork.Testnet;
}