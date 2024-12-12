using System.Collections.ObjectModel;

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

public class WalletDesign : IWallet
{
    public IEnumerable<ITransaction> History { get; } =
    [
        new TransactionDesign() { Address = "someaddress1", Amount = 0.0001m, UtxoCount = 12, Path = "path", ViewRawJson = "json"}, 
        new TransactionDesign() { Address = "someaddress2", Amount = 0.0003m, UtxoCount = 15, Path = "path", ViewRawJson = "json"},
        new TransactionDesign() { Address = "someaddress3", Amount = 0.0042m, UtxoCount = 15, Path = "path", ViewRawJson = "json"},
        new TransactionDesign() { Address = "someaddress4", Amount = 0.00581m, UtxoCount = 15, Path = "path", ViewRawJson = "json"},
    ];
}

public class TransactionDesign : ITransaction
{
    public string Address { get; set; }
    public decimal Amount { get; set; }
    public string Path { get; set; }
    public int UtxoCount { get; set; }
    public string ViewRawJson { get; set; }
}

public interface IWallet
{
    public IEnumerable<ITransaction> History { get; }
}

public class WalletModel : IWallet
{
    public IEnumerable<ITransaction> History { get; }
}

public interface ITransaction
{
    public string Address { get; }
    public decimal Amount { get; }
    public string Path { get; }
    public int UtxoCount { get; }
    public string ViewRawJson { get; }
}