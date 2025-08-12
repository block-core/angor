using System.Windows.Input;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Home;

public interface IHomeSectionViewModel
{
    public bool IsWalletSetup { get; }
    public ICommand GoToWalletSection { get; }
    public ICommand GoToFounderSection { get; }
    public ICommand OpenHub { get; }
    public IEnhancedCommand GoToAngorFlow { get; set; }
}