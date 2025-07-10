using System.Linq;
using Avalonia.Controls.Selection;
using ReactiveUI.SourceGenerators;
using Zafiro.Avalonia.Misc;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ManageFunds;

public interface IStageClaimViewModel
{
    public IEnumerable<IClaimableStage> ClaimableStages { get; }
}

public interface IClaimableStage
{
    public int ClaimableTransactions { get; }
    public IEnumerable<IClaimableTransaction> Transactions { get; }
    public IAmountUI ClaimableAmount { get; }
    public SelectionModel<IClaimableTransaction> Selection { get; }
    IEnhancedCommand Claim { get; }
    IEnhancedCommand SelectSpendable { get; }
    ReactiveSelection<IClaimableTransaction, string> ReactiveSelection { get; }
}

public interface IClaimableTransaction
{
    public IAmountUI Amount { get; }
    public string Address { get; }
    public ClaimStatus ClaimStatus { get; }
}

public class ClaimableTransactionDesign : IClaimableTransaction
{
    public IAmountUI Amount { get; set; } = new AmountUI(100000); 
    public string Address { get; set; }  = "bc1qexampleaddress"; 
    public ClaimStatus ClaimStatus { get; set; } = ClaimStatus.Unspent;
}

public partial class ClaimableStageDesign : ReactiveObject, IClaimableStage
{
    public ClaimableStageDesign()
    {
        ReactiveSelection = new ReactiveSelection<IClaimableTransaction, string>(x => x.Address);
        
        var selectedCountChanged = this.WhenAnyValue(design => design.ReactiveSelection.SelectedItems.Count);
        
        Claim = ReactiveCommand.Create(() => { }, selectedCountChanged.Select(i => i > 0)).Enhance();

        claimableAmountHelper = this.WhenAnyValue(design => design.Transactions)
            .WhereNotNull()
            .Select(txn =>
            {
                var claimable = txn.Where(transaction => transaction.ClaimStatus == ClaimStatus.Unspent).ToList();
                return new AmountUI(claimable
                    .Select(transaction => transaction.Amount.Sats)
                    .DefaultIfEmpty()
                    .Sum());
            }).ToProperty(this, design => design.ClaimableAmount);
        
        claimableTransactionsHelper = this.WhenAnyValue(design => design.Transactions)
            .WhereNotNull()
            .Select(txn =>
            {
                var claimable = txn.Where(transaction => transaction.ClaimStatus == ClaimStatus.Unspent).ToList();
                return claimable.Count;
            }).ToProperty(this, design => design.ClaimableTransactions);
        
        Transactions = new List<IClaimableTransaction>();
    }

    public ReactiveSelection<IClaimableTransaction, string> ReactiveSelection { get; }


    [ObservableAsProperty]
    private int claimableTransactions;

    [ReactiveUI.SourceGenerators.Reactive]
    private IEnumerable<IClaimableTransaction> transactions;
    [ObservableAsProperty]
    private IAmountUI claimableAmount;

    public SelectionModel<IClaimableTransaction> Selection { get; } = new() { SingleSelect = false };
    public IEnhancedCommand Claim { get; }
    public IEnhancedCommand SelectSpendable { get; }
}

public enum ClaimStatus
{
    Invalid = 0,
    Unspent,
    Pending,
    SpentByFounder,
    WithdrawByInvestor
}