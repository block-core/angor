using System.Linq;
using Avalonia.Controls.Selection;
using ReactiveUI.SourceGenerators;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ManageFunds;

public interface IStageClaimViewModel
{
    public IEnumerable<IClaimableStage> ClaimableStages { get; }
    public DateTime EstimatedCompletion { get; }
}

public interface IClaimableStage
{
    public int ClaimableTransactions { get; }
    public IEnumerable<IClaimableTransaction> Transactions { get; }
    public IAmountUI ClaimableAmount { get; }
    public SelectionModel<IClaimableTransaction> Selection { get; }
    IEnhancedCommand Claim { get; }
}

public interface IClaimableTransaction
{
    public IAmountUI Amount { get; }
    public string Address { get; }
    public ClaimStatus ClaimStatus { get; }
}

public class ClaimableTransactionDesign : IClaimableTransaction
{
    public IAmountUI Amount { get; set; }
    public string Address { get; set; }
    public ClaimStatus ClaimStatus { get; set; }
}

public partial class ClaimableStageDesign : ReactiveObject, IClaimableStage
{
    public ClaimableStageDesign()
    {
        var selectedCountChanged = Observable.FromEventPattern<SelectionModelSelectionChangedEventArgs<IClaimableTransaction>>(h => Selection.SelectionChanged += h, h => Selection.SelectionChanged -= h)
            .Select(_ => Selection.Count);
        
        Claim = ReactiveCommand.Create(() => { }, selectedCountChanged.Select(i => i > 0)).Enhance();
        

        _claimableAmountHelper = this.WhenAnyValue(design => design.Transactions)
            .WhereNotNull()
            .Select(txn =>
            {
                var claimableTransactions = txn.Where(transaction => transaction.ClaimStatus == ClaimStatus.Unspent).ToList();
                return new AmountUI(claimableTransactions
                    .Select(transaction => transaction.Amount.Sats)
                    .DefaultIfEmpty()
                    .Sum());
            }).ToProperty(this, design => design.ClaimableAmount);
        
        _claimableTransactionsHelper = this.WhenAnyValue(design => design.Transactions)
            .WhereNotNull()
            .Select(txn =>
            {
                var claimableTransactions = txn.Where(transaction => transaction.ClaimStatus == ClaimStatus.Unspent).ToList();
                return claimableTransactions.Count;
            }).ToProperty(this, design => design.ClaimableTransactions);
        
        Transactions = new List<IClaimableTransaction>();
    }

    [ObservableAsProperty]
    private int _claimableTransactions;

    [ReactiveUI.SourceGenerators.Reactive]
    private IEnumerable<IClaimableTransaction> _transactions;
    [ObservableAsProperty]
    private IAmountUI _claimableAmount;

    public SelectionModel<IClaimableTransaction> Selection { get; } = new() { SingleSelect = false };
    public IEnhancedCommand Claim { get; }
}

public enum ClaimStatus
{
    Invalid = 0,
    Unspent,
    Pending,
    SpentByFounder,
    WithdrawByInvestor
}