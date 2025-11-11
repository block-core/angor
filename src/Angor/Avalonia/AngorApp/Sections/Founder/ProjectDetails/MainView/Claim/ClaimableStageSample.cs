using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.Selection;
using ReactiveUI.SourceGenerators;
using Zafiro.Avalonia.Misc;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ProjectDetails.MainView.Claim;

public partial class ClaimableStageSample : ReactiveObject, IClaimableStage
{
    public ClaimableStageSample()
    {
        ReactiveSelection = new ReactiveSelection<IClaimableTransaction, string>(new SelectionModel<IClaimableTransaction>()
        {
            SingleSelect = false
        }, x => x.Address, transaction => transaction.IsClaimable);
        
        var selectedCountChanged = this.WhenAnyValue(design => design.ReactiveSelection.SelectedItems.Count);
        
        Claim = ReactiveCommand.CreateFromTask(() => Task.FromResult(Maybe.From(Result.Success())), selectedCountChanged.Select(i => i > 0)).Enhance();

        claimableAmountHelper = this.WhenAnyValue<ClaimableStageSample, IEnumerable<IClaimableTransaction>>(design => design.Transactions)
            .WhereNotNull()
            .Select(txn =>
            {
                var claimable = txn.Where(transaction => transaction.IsClaimable).ToList();
                return new AmountUI(claimable
                    .Select(transaction => transaction.Amount.Sats)
                    .DefaultIfEmpty()
                    .Sum());
            }).ToProperty<ClaimableStageSample, IAmountUI>(this, design => design.ClaimableAmount);
        
        claimableTransactionsCountHelper = this.WhenAnyValue<ClaimableStageSample, IEnumerable<IClaimableTransaction>>(design => design.Transactions)
            .WhereNotNull()
            .Select(txn =>
            {
                var claimable = txn.Where(transaction => transaction.IsClaimable).ToList();
                return claimable.Count;
            }).ToProperty(this, design => design.ClaimableTransactionsCount);
        
        Transactions = new List<IClaimableTransaction>();
    }

    public ReactiveSelection<IClaimableTransaction, string> ReactiveSelection { get; }


    [ObservableAsProperty]
    private int claimableTransactionsCount;

    [Reactive]
    private IEnumerable<IClaimableTransaction> transactions;
    [ObservableAsProperty]
    private IAmountUI claimableAmount;

    public IEnhancedCommand<Maybe<Result>> Claim { get; }
}