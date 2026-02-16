using Avalonia.Controls.Selection;
using Zafiro.Avalonia.Misc;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Release.ReleaseDialog
{
    public class ReleaseDialogViewModelSample : IReleaseDialogViewModel
    {
        public ReleaseDialogViewModelSample()
        {
            SelectionModel<IReleaseDialogItem> selectionModel = new(Items) { SingleSelect = false };
            selectionModel.SelectAll();
            Selection = new ReactiveSelection<IReleaseDialogItem, string>(selectionModel, x => x.Address);
        }

        public IEnumerable<IReleaseDialogItem> Items { get; } =
        [
            new ReleaseDialogItemSample(
                AmountUI.FromBtc(0.093824),
                "zap1a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8"),
            new ReleaseDialogItemSample(
                AmountUI.FromBtc(0.082096),
                "zap2b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9"),
            new ReleaseDialogItemSample(
                AmountUI.FromBtc(0.05864),
                "zap3c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0")
        ];

        public ReactiveSelection<IReleaseDialogItem, string> Selection { get; }
    }
}