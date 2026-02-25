using Zafiro.Avalonia.Misc;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Release.ReleaseDialog
{
    public interface IReleaseDialogViewModel
    {
        IList<IReleaseDialogItem> Items { get; }
        ReactiveSelection<IReleaseDialogItem, string> Selection { get; }
    }
}