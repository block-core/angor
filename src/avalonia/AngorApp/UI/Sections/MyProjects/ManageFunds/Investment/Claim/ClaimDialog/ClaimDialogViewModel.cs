using AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Claim.Transactions;
using Avalonia.Controls.Selection;
using Zafiro.Avalonia.Misc;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Claim.ClaimDialog
{
    public class ClaimDialogViewModel : IClaimDialogViewModel, IHaveTitle
    {
        public ClaimDialogViewModel(string title, string message, ICollection<ITransaction> transactions)
        {
            SelectionModel<ITransaction> selectionModel = new(transactions) { SingleSelect = false };
            SelectedItems = new ReactiveSelection<ITransaction, string>(selectionModel, x => x.Address);
            Items = transactions;
            Title = Observable.Return(title);
            Message = message;
            selectionModel.SelectAll();
        }

        public IEnumerable<ITransaction> Items { get; }
        public ReactiveSelection<ITransaction, string> SelectedItems { get; }
        public string Message { get; }
        public IObservable<string> Title { get; }
    }
}
