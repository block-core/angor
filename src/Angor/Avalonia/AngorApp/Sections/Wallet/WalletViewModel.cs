using System.Threading.Tasks;
using CSharpFunctionalExtensions;

namespace AngorApp.Sections.Wallet;

public class WalletViewModel : ReactiveObject, IWalletViewModel
{
    public IWallet Wallet { get; set; }
}

public interface IWalletViewModel
{
    public IWallet Wallet { get; set; }
}

public class WalletViewModelDesign : IWalletViewModel
{
    public IWallet Wallet { get; set; } = new WalletDesign();
}

public class TransactionDesign : ITransaction
{
    public string Address { get; set; }
    public decimal Amount { get; set; }
    public string Path { get; set; }
    public int UtxoCount { get; set; }
    public string ViewRawJson { get; set; }

    public async Task<Result> Broadcast()
    {
        await Task.Delay(4000);
        return Result.Success();
    }
}

public interface IWallet
{
    public IEnumerable<ITransaction> History { get; }
    decimal? Balance { get; set; }
    Task<ITransaction> CreateTransaction(decimal amount, string address);
}

public interface ITransaction
{
    public string Address { get; }
    public decimal Amount { get; }
    public string Path { get; }
    public int UtxoCount { get; }
    public string ViewRawJson { get; }
    Task<Result> Broadcast();
}