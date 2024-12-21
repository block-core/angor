using System.Threading.Tasks;

namespace AngorApp.Sections.Wallet;

public class WalletDesign : IWallet
{
    public IEnumerable<ITransaction> History { get; } =
    [
        new TransactionDesign { Address = "someaddress1", Amount = 0.0001m, UtxoCount = 12, Path = "path", ViewRawJson = "json" },
        new TransactionDesign { Address = "someaddress2", Amount = 0.0003m, UtxoCount = 15, Path = "path", ViewRawJson = "json" },
        new TransactionDesign { Address = "someaddress3", Amount = 0.0042m, UtxoCount = 15, Path = "path", ViewRawJson = "json" },
        new TransactionDesign { Address = "someaddress4", Amount = 0.00581m, UtxoCount = 15, Path = "path", ViewRawJson = "json" }
    ];

    public decimal? Balance { get; set; } = 2m;

    public async Task<ITransaction> CreateTransaction(decimal amount, string address)
    {
        await Task.Delay(3000);

        //return Result.Failure<ITransaction>("Transaction creation failed");
        return new TransactionDesign
        {
            Address = address,
            Amount = amount
        };
    }
}