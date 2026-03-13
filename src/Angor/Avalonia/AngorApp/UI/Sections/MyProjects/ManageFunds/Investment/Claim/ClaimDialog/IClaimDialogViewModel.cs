using AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Claim.Transactions;
using Zafiro.Avalonia.Misc;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Claim.ClaimDialog
{
    public interface IClaimDialogViewModel
    {
        IEnumerable<ITransaction> Items { get; }
        ReactiveSelection<ITransaction, string> SelectedItems { get; }
        string Message { get; }
    }
}
