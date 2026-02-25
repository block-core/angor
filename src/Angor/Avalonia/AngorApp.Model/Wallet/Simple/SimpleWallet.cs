using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Application;
using Angor.Shared;
using Angor.Shared.Models;
using AngorApp.Model.Amounts;
using AngorApp.Model.Common;
using Blockcore.Networks;
using CSharpFunctionalExtensions;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Zafiro.Avalonia.Dialogs;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;
using Zafiro.UI.Commands;

namespace AngorApp.Model.Wallet.Simple;

public partial class SimpleWallet : ReactiveObject, IWallet, IDisposable
{
    private readonly IWalletAppService walletAppService;
    private readonly IDialog dialog;
    [ObservableAsProperty] private IAmountUI balance = new AmountUI(0);
    [ObservableAsProperty] private IAmountUI unconfirmedBalance = new AmountUI(0);
    [ObservableAsProperty] private IAmountUI reservedBalance = new AmountUI(0);
    private readonly CompositeDisposable disposable = new();

    public SimpleWallet(WalletId id, IWalletAppService walletAppService, ISendMoneyFlow sendMoneyFlow, IDialog dialog, INotificationService notificationService, INetworkConfiguration networkConfiguration, AccountBalanceInfo? cachedBalance = null)
    {
        this.walletAppService = walletAppService;
        this.dialog = dialog;
        Id = id;
        var transactionCollection = CreateTransactions(id).DisposeWith(disposable);

        RefreshBalanceAndFetchHistory = transactionCollection.Refresh;
        RefreshBalanceAndFetchHistory.HandleErrorsWith(notificationService, "Cannot load wallet info");

        // RefreshBalance: Refresh balance info without fetching transaction history
        RefreshBalance = ReactiveCommand.CreateFromTask(RefreshAndGetAccountBalanceInfo).Enhance().DisposeWith(disposable);
        RefreshBalance.HandleErrorsWith(notificationService, "Cannot refresh balance");

        // Balance updates from both RefreshBalanceAndFetchHistory (with history) and RefreshBalance (without history)
        var balanceFromLoad = RefreshBalanceAndFetchHistory.Successes()
            .SelectMany(_ => Observable.FromAsync(() => walletAppService.GetAccountBalanceInfo(id)))
            .Where(r => r.IsSuccess)
            .Select(r => r.Value);

        var balanceFromRefresh = RefreshBalance.Successes();

        var balanceStream = balanceFromLoad.Merge(balanceFromRefresh);

        // Seed with cached balance from DB so we don't show 0 on startup
        var balanceUpdates = (cachedBalance != null ? balanceStream.StartWith(cachedBalance) : balanceStream)
            .Publish()
            .RefCount();

        balanceHelper = balanceUpdates
            .Select(info => new AmountUI(info.TotalBalance))
            .ToProperty<SimpleWallet, IAmountUI>(this, x => x.Balance)
            .DisposeWith(disposable);

        unconfirmedBalanceHelper = balanceUpdates
            .Select(info => new AmountUI(info.TotalUnconfirmedBalance))
            .ToProperty<SimpleWallet, IAmountUI>(this, x => x.UnconfirmedBalance)
            .DisposeWith(disposable);

        reservedBalanceHelper = balanceUpdates
            .Select(info => new AmountUI(info.TotalBalanceReserved))
            .ToProperty<SimpleWallet, IAmountUI>(this, x => x.ReservedBalance)
            .DisposeWith(disposable);

        History = transactionCollection.Items;

        Send = ReactiveCommand.CreateFromTask(() => sendMoneyFlow.SendMoney(this)).Enhance().DisposeWith(disposable);
        
        GetReceiveAddress = ReactiveCommand.CreateFromTask(GenerateReceiveAddress).Enhance().DisposeWith(disposable);
        ReceiveAddress = GetReceiveAddress.Successes().Publish().RefCount();
        
        GetTestCoins = ReactiveCommand.CreateFromTask(RequestTestCoins).Enhance().DisposeWith(disposable);

        GetTestCoins.HandleErrorsWith(notificationService, "Cannot get test coins");

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
    public string Name { get; } = "Bitcoin Wallet";
    public IEnhancedCommand Send { get; }
    public IEnhancedCommand<Result<string>> GetReceiveAddress { get; }
    public IEnhancedCommand<Result<IEnumerable<IBroadcastedTransaction>>> RefreshBalanceAndFetchHistory { get; }
    public IEnhancedCommand<Result<AccountBalanceInfo>> RefreshBalance { get; }
    public ReadOnlyObservableCollection<IBroadcastedTransaction> History { get; }
    public IEnhancedCommand<Result> GetTestCoins { get; }
    public DateTimeOffset CreatedOn { get; } = DateTimeOffset.MinValue;

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

    private Task<Result<AccountBalanceInfo>> RefreshAndGetAccountBalanceInfo()
    {
        return walletAppService.RefreshAndGetAccountBalanceInfo(Id);
    }

    private async Task<Result> RequestTestCoins()
    {
        var result = await walletAppService.GetTestCoins(Id);
        
        if (result.IsSuccess)
        {
            // Reload the transactions after getting test coins
            await RefreshBalanceAndFetchHistory.Execute();
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
