using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;
using Angor.UI.Model.Flows;
using Angor.UI.Model.Implementation.Common;
using CSharpFunctionalExtensions;
using DynamicData;
using DynamicData.Aggregation;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Zafiro.Avalonia.Dialogs;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI;
using Zafiro.UI.Commands;

namespace Angor.UI.Model.Implementation.Wallet.Simple;

public partial class SimpleWallet : ReactiveObject, IWallet, IDisposable
{
    private readonly IWalletAppService walletAppService;
    [ObservableAsProperty] private IAmountUI balance;
    private readonly CompositeDisposable disposable = new();

    public SimpleWallet(WalletId id, IWalletAppService walletAppService, ISendMoneyFlow sendMoneyFlow, INotificationService notificationService)
    {
        this.walletAppService = walletAppService;
        Id = id;
        var transactionsCollection1 = RefreshableCollection.Create(
                () => walletAppService.GetTransactions(id)
                    .Map(transactions => transactions.Select<BroadcastedTransaction, IBroadcastedTransaction>(transaction => new HistoryTransaction(transaction))),
                transaction => transaction.Id)
            .DisposeWith(disposable);

        Load = transactionsCollection1.Refresh;
        Load.HandleErrorsWith(notificationService, "Cannot load wallet info");

        balanceHelper = transactionsCollection1.Changes
            .Sum(transaction => transaction.Balance.Sats)
            .Select(sats => new AmountUI(sats))
            .ToProperty(this, x => x.Balance)
            .DisposeWith(disposable);

        History = transactionsCollection1.Items;

        Send = ReactiveCommand.CreateFromTask(() => sendMoneyFlow.SendMoney(this)).Enhance().DisposeWith(disposable);
        
        GetReceiveAddress = ReactiveCommand.CreateFromTask(GenerateReceiveAddress).Enhance().DisposeWith(disposable);
        ReceiveAddress = GetReceiveAddress.Successes().Publish().RefCount();
    }

    public IObservable<string> ReceiveAddress { get; }
    public IEnhancedCommand Send { get; }
    public IEnhancedCommand<Result<string>> GetReceiveAddress { get; }
    public IEnhancedCommand<Result<IEnumerable<IBroadcastedTransaction>>> Load { get; }
    public ReadOnlyObservableCollection<IBroadcastedTransaction> History { get; }

    public Result IsAddressValid(string address)
    {
        return Result.Success();
    }

    public WalletId Id { get; }

    public Task<Result<string>> GenerateReceiveAddress()
    {
        return walletAppService.GetNextReceiveAddress(Id).Map(address => address.Value);
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
