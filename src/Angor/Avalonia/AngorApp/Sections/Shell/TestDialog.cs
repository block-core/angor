using System.Threading.Tasks;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.Sections.Shell;

public class TestDialog : IDialog
{
    public Task<bool> Show(object viewModel, string title, Func<ICloseable, IEnumerable<IOption>> optionsFactory)
    {
        return Task.FromResult(false);
    }
}