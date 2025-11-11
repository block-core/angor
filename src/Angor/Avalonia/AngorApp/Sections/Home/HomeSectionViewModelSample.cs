using System.Windows.Input;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Home;

public class HomeSectionViewModelSample : IHomeSectionViewModel
{
    public bool IsWalletSetup { get; set; }
    public ICommand GoToWalletSection { get; }
    public ICommand GoToFounderSection { get; }
    public ICommand OpenHub { get; }
    public IEnhancedCommand GoToAngorFlow { get; set; }
    public ReactiveCommand<string, Unit> GoToSection { get; }
}