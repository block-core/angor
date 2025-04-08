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
    private readonly IWalletAppService walletAppService;
    private readonly CompositeDisposable disposables = new();
    
    public DynamicWallet(WalletId walletId, IWalletAppService walletAppService, ITransactionWatcher transactionWatcher)
    {
        Id = walletId;
        this.walletAppService = walletAppService;

        var transactionsSource = new SourceCache<BroadcastedTransaction, string>(x => x.Id);

        var changes = transactionsSource.Connect();

        SyncCommand = StoppableCommand.Create(() => transactionWatcher.Watch(Id), Maybe<IObservable<bool>>.None);
        
        transactionsSource.PopulateFrom(SyncCommand.StartReactive.Successes());

        changes
            .Transform(transaction => (IBroadcastedTransaction)new BroadcastedTransactionImpl(transaction))
            .Bind(out var transactions)
            .Subscribe()
            .DisposeWith(disposables);

        History = transactions;

        Balance = changes.Sum(x => x.Balance.Value);
        HasBalance = Balance.Any().StartWith(false);
        HasTransactions = SyncCommand.StartReactive.Any().StartWith(false);
    }

    public IObservable<bool> HasTransactions { get;  }

    public IObservable<bool> HasBalance { get; }

    public IObservable<long> Balance { get; }

    public StoppableCommand<Unit, Result<BroadcastedTransaction>> SyncCommand { get; }

    [ObservableAsProperty] private ResultViewModel loadResult;


    public Task<Result<string>> GenerateReceiveAddress()
    {
        return walletAppService.GetNextReceiveAddress(Id).Map(x => x.Value);
    }
    
    public ReadOnlyObservableCollection<IBroadcastedTransaction> History { get; }

    public BitcoinNetwork Network { get; } = BitcoinNetwork.Testnet;

    public Task<Result<ITransactionDraft>> CreateDraft(long amount, string address, long feerate)
    {
        return walletAppService.EstimateFee(Id, new Amount(amount), new Address(address), new DomainFeeRate(feerate))
            .Map(ITransactionDraft (fee) => new TransactionDraft(Id, amount, address, feerate, fee, walletAppService));
    }

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