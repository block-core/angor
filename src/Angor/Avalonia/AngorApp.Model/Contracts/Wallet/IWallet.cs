using System.Collections.ObjectModel;
using Angor.Contexts.Wallet.Domain;
using CSharpFunctionalExtensions;
using Zafiro.UI.Commands;

namespace AngorApp.Model.Contracts.Wallet;

public interface IWallet
{
    public ReadOnlyObservableCollection<IBroadcastedTransaction> History { get; }
    IAmountUI Balance { get; }
    Result IsAddressValid(string address);
    WalletId Id { get; }
    IEnhancedCommand Send { get; }
    public IEnhancedCommand<Result<string>> GetReceiveAddress { get; }
    public Task<Result<string>> GenerateReceiveAddress();
    public IEnhancedCommand GetTestCoins { get; }
}