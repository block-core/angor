using System.Collections.ObjectModel;
using Angor.Contexts.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace Angor.UI.Model;

public interface IWallet
{
    public ReadOnlyObservableCollection<IBroadcastedTransaction> History { get; }
    IAmountUI Balance { get; }
    Result IsAddressValid(string address);
    WalletId Id { get; }
    public Task<Result<string>> GenerateReceiveAddress();
}