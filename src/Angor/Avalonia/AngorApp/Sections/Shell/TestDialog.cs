using System.Threading.Tasks;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Dialogs.Simple;

namespace AngorApp.Sections.Shell;

public class TestDialog : IDialog
{
    public Task<bool> Show(object viewModel, string title, Func<ICloseable, IOption[]> optionsFactory)
    {
        return Task.FromResult(false);
    }
}