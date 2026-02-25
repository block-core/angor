using Avalonia.Controls.Selection;
using Zafiro.Avalonia.Misc;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Release.ReleaseDialog
{
    public class ReleaseDialogViewModel : IReleaseDialogViewModel
    {
        public ReleaseDialogViewModel(IEnumerable<IReleaseDialogItem> items)
        {
            Items = items.ToList();
            SelectionModel<IReleaseDialogItem> selectionModel = new(Items) { SingleSelect = false };
            selectionModel.SelectAll();
            Selection = new ReactiveSelection<IReleaseDialogItem, string>(selectionModel, x => x.InvestmentEventId);
        }

        public IList<IReleaseDialogItem> Items { get; }

        public ReactiveSelection<IReleaseDialogItem, string> Selection { get; }
    }
}