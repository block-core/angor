using Avalonia.Controls.Selection;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Claim.Transactions;
using Zafiro.Avalonia.Misc;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Claim.ClaimDialog
{
    public class ClaimDialogViewModel : IClaimDialogViewModel, IHaveTitle
    {
        public ClaimDialogViewModel(string title, string message, ICollection<IClaimableTransaction> transactions)
        {
            SelectionModel<IClaimableTransaction> selectionModel = new(transactions) { SingleSelect = false };
            SelectedItems = new ReactiveSelection<IClaimableTransaction, string>(selectionModel, x => x.Address);
            Items = transactions;
            Title = Observable.Return(title);
            Message = message;
            selectionModel.SelectAll();
        }

        public IEnumerable<IClaimableTransaction> Items { get; }
        public ReactiveSelection<IClaimableTransaction, string> SelectedItems { get; }
        public string Message { get; }
        public IObservable<string> Title { get; }
    }
}
