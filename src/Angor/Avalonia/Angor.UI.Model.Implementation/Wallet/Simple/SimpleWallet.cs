using Angor.Contexts.Wallet.Application;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Angor.Contexts.Wallet.Domain;
using Angor.Shared;
using Angor.UI.Model.Flows;
using Angor.UI.Model.Implementation.Common;
using Blockcore.Networks;
using CSharpFunctionalExtensions;
using DynamicData.Aggregation;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Zafiro.Avalonia.Dialogs;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;
using Zafiro.UI.Commands;

namespace Angor.UI.Model.Implementation.Wallet.Simple;

public partial class SimpleWallet : ReactiveObject, IWallet, IDisposable
{
    private readonly IWalletAppService walletAppService;
    private readonly IDialog dialog;
    [ObservableAsProperty] private IAmountUI balance = new AmountUI(0);
    private readonly CompositeDisposable disposable = new();

    public SimpleWallet(WalletId id, IWalletAppService walletAppService, ISendMoneyFlow sendMoneyFlow, IDialog dialog, INotificationService notificationService, INetworkConfiguration networkConfiguration)
    {
        this.walletAppService = walletAppService;
        this.dialog = dialog;
        Id = id;
        var transactionCollection = CreateTransactions(id)
            .DisposeWith(disposable);

        Load = transactionCollection.Refresh;
        Load.HandleErrorsWith(notificationService, "Cannot load wallet info");

        balanceHelper = transactionCollection.Changes
            .Sum(transaction => transaction.Balance.Sats)
            .Select(sats => new AmountUI(sats))
            .ToProperty(this, x => x.Balance)
            .DisposeWith(disposable);

        History = transactionCollection.Items;

        Send = ReactiveCommand.CreateFromTask(() => sendMoneyFlow.SendMoney(this)).Enhance().DisposeWith(disposable);
        
        GetReceiveAddress = ReactiveCommand.CreateFromTask(GenerateReceiveAddress).Enhance().DisposeWith(disposable);
        ReceiveAddress = GetReceiveAddress.Successes().Publish().RefCount();
        
        GetTestCoins = ReactiveCommand.CreateFromTask(RequestTestCoins).Enhance().DisposeWith(disposable);
        
        CanGetTestCoins = networkConfiguration.GetNetwork().NetworkType == NetworkType.Testnet;
    }

    private RefreshableCollection<IBroadcastedTransaction, string> CreateTransactions(WalletId id)
    {
        return RefreshableCollection.Create(
            () => walletAppService.GetTransactions(id)
                .Map(transactions => transactions.Select(IBroadcastedTransaction (transaction) => new HistoryTransaction(transaction, dialog))),
            getKey: transaction => transaction.Id);
    }

    public IObservable<string> ReceiveAddress { get; }
    public IEnhancedCommand Send { get; }
    public IEnhancedCommand<Result<string>> GetReceiveAddress { get; }
    public IEnhancedCommand<Result<IEnumerable<IBroadcastedTransaction>>> Load { get; }
    public ReadOnlyObservableCollection<IBroadcastedTransaction> History { get; }
    public IEnhancedCommand GetTestCoins { get; }

    public Result IsAddressValid(string address)
    {
        return Result.Success();
    }

    public WalletId Id { get; }

    public bool CanGetTestCoins { get; }

    public Task<Result<string>> GenerateReceiveAddress()
    {
        return walletAppService.GetNextReceiveAddress(Id).Map(address => address.Value);
    }

    private async Task<Result> RequestTestCoins()
    {
        var result = await walletAppService.GetTestCoins(Id);
        
        if (result.IsSuccess)
        {
            // Reload the transactions after getting test coins
            await Load.Execute();
        }
        
        return result;
    }

    protected bool Equals(SimpleWallet other)
    {
        return Id.Equals(other.Id);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Equals((SimpleWallet)obj);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public void Dispose()
    {
        disposable.Dispose();
    }
}
