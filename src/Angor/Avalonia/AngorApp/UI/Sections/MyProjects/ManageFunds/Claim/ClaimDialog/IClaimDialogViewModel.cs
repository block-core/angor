using Zafiro.Avalonia.Misc;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Claim.Transactions;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Claim.ClaimDialog
{
    public interface IClaimDialogViewModel
    {
        IEnumerable<IClaimableTransaction> Items { get; }
        ReactiveSelection<IClaimableTransaction, string> SelectedItems { get; }
        string Message { get; }
    }
}
