using System.Collections.ObjectModel;
using System.Reactive;
using Angor.Contexts.Wallet.Domain;
using CSharpFunctionalExtensions;
using Zafiro.UI;

namespace Angor.UI.Model;

public interface IWallet
{
    public ReadOnlyObservableCollection<IBroadcastedTransaction> History { get; }
    IObservable<long> Balance { get; }
    Result IsAddressValid(string address);
    WalletId Id { get; }
    public Task<Result<string>> GenerateReceiveAddress();
}