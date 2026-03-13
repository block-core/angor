using AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Claim.Transactions;
using Avalonia.Controls.Selection;
using Zafiro.Avalonia.Misc;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Claim.ClaimDialog
{
    public class ClaimDialogViewModelSample : IClaimDialogViewModel
    {
        public ClaimDialogViewModelSample()
        {
            SelectionModel<ITransaction> selectionModel = new(Items) { SingleSelect = false };
            selectionModel.SelectAll();
            SelectedItems = new ReactiveSelection<ITransaction, string>(selectionModel, x => x.Address);
        }

        public IEnumerable<ITransaction> Items { get; } =
        [
            new TransactionSample
            {
                Amount = AmountUI.FromBtc(0.093824),
                Address = "zap1a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8"
            },
            new TransactionSample
            {
                Amount = AmountUI.FromBtc(0.082096),
                Address = "zap2b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9"
            },
            new TransactionSample
            {
                Amount = AmountUI.FromBtc(0.05864),
                Address = "zap3c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0"
            }
        ];

        public ReactiveSelection<ITransaction, string> SelectedItems { get; }

        public string Message { get; set; } =
            "This is sample message for the claim dialog. It can be used to provide additional information to the user about the claim process, such as instructions or warnings.";
    }
}
