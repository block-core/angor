using Zafiro.Avalonia.Controls.Shell;

namespace AngorApp.UI.Sections.Shell;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        
        MessageBus.Current.SendMessage(Shell);
    }
}