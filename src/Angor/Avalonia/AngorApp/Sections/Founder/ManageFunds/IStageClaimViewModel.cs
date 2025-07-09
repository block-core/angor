using Avalonia.Controls.Selection;

namespace AngorApp.Sections.Founder.ManageFunds;

public interface IStageClaimViewModel
{
    public SelectionModel<IClaimableStage> SelectionModel { get; }
    public IEnumerable<IClaimableStage> ClaimableStages { get; }
    public DateTime EstimatedCompletion { get; }
}

public interface IClaimableStage
{
    
    public int ClaimableTransactions { get; }
    public IEnumerable<IClaimableTransaction> Transactions { get; }
    public IAmountUI ClaimableAmount { get; }
    public SelectionModel<IClaimableTransaction> Selection { get; }
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

public class ClaimableStageDesign : IClaimableStage
{
    public int ClaimableTransactions { get; set; }
    public IEnumerable<IClaimableTransaction> Transactions { get; set; }
    public IAmountUI ClaimableAmount { get; set; }
    public SelectionModel<IClaimableTransaction> Selection { get; } = new SelectionModel<IClaimableTransaction>(){ SingleSelect = false };
}

public enum ClaimStatus
{
    Invalid = 0,
}