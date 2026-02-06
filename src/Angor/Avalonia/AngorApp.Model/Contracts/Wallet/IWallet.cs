using System.Collections.ObjectModel;
using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Domain;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using Zafiro.UI.Commands;

namespace AngorApp.Model.Contracts.Wallet;

public interface IWallet
{
    public ReadOnlyObservableCollection<IBroadcastedTransaction> History { get; }
    IAmountUI Balance { get; }
    IAmountUI UnconfirmedBalance { get; }
    IAmountUI ReservedBalance { get; }
    Result IsAddressValid(string address);
    WalletId Id { get; }
    public string Name { get; }
    IEnhancedCommand Send { get; }
    public IEnhancedCommand<Result<string>> GetReceiveAddress { get; }
    public Task<Result<string>> GenerateReceiveAddress();
    public IEnhancedCommand<Result> GetTestCoins { get; }
    public bool CanGetTestCoins { get; }
    IEnhancedCommand<Result<IEnumerable<IBroadcastedTransaction>>> RefreshBalanceAndFetchHistory { get; }
    IEnhancedCommand<Result<AccountBalanceInfo>> RefreshBalance { get; }
    IEnumerable<string> Labels => [];
    NetworkKind NetworkKind => NetworkKind.Bitcoin;
    ImportKind ImportKind => ImportKind.Generated; 
    public DateTimeOffset CreatedOn { get; } 
}