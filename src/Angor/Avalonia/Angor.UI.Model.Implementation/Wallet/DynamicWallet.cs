using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;
using CSharpFunctionalExtensions;
using DynamicData;
using DynamicData.Aggregation;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;

namespace Angor.UI.Model.Implementation.Wallet;

public partial class DynamicWallet : ReactiveObject, IWallet, IDisposable
{
    [ObservableAsProperty]
    private IAmountUI balance;
    [ObservableAsProperty] private ResultViewModel loadResult;
    
    private readonly IWalletAppService walletAppService;
    private readonly CompositeDisposable disposables = new();
    
    public DynamicWallet(WalletId walletId, IWalletAppService walletAppService, ITransactionWatcher transactionWatcher)
    {
        Id = walletId;
        this.walletAppService = walletAppService;

        var transactionsSource = new SourceCache<BroadcastedTransaction, string>(x => x.Id);

        var changes = transactionsSource.Connect(suppressEmptyChangeSets: false);

        Sync = StoppableCommand.Create(() => transactionWatcher.Watch(Id), Maybe<IObservable<bool>>.None);
        
        transactionsSource.PopulateFrom(Sync.StartReactive.Successes().OfType<TransactionEvent>().Select(ev => ev.Transaction));

        changes
            .Transform(transaction => (IBroadcastedTransaction)new BroadcastedTransactionImpl(transaction))
            .Bind(out var transactions)
            .Subscribe()
            .DisposeWith(disposables);

        History = transactions;

        balanceHelper = changes
            .Sum(transaction => transaction.GetBalance().Sats)
            .Select(l => new AmountUI(l))
            .ToProperty(this, x => x.Balance);
    }
    public StoppableCommand<Unit, Result<Event>> Sync { get; }

    public Task<Result<string>> GenerateReceiveAddress()
    {
        return walletAppService.GetNextReceiveAddress(Id).Map(x => x.Value);
    }
    
    public ReadOnlyObservableCollection<IBroadcastedTransaction> History { get; }

    public BitcoinNetwork Network { get; } = BitcoinNetwork.Testnet;

    public Result IsAddressValid(string address)
    {
        return Result.Success()
            .Ensure(() =>
            {
                var validateBitcoinAddress = BitcoinAddressValidator.ValidateBitcoinAddress(address, Network);
                return validateBitcoinAddress.Network == Network;
            }, "Network mismatch");
    }

    public WalletId Id { get; }

    public void Dispose()
    {
        disposables.Dispose();
        loadResultHelper.Dispose();
    }
}

