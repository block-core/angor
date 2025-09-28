using AngorApp.UI.Services;
using Avalonia;
using Avalonia.LogicalTree;
using ReactiveUI.SourceGenerators;
using Zafiro.Avalonia.Controls.Wizards.Slim;
using Zafiro.UI.Commands;
using Zafiro.UI.Wizards.Slim;

namespace AngorApp.Sections.Shell;

public partial class HeaderViewModel : ReactiveObject
{
    private readonly Blockcore.Networks.Network network;
    private readonly ObservableAsPropertyHelper<object?> currentHeader;

    public HeaderViewModel(IEnhancedCommand back, object content, Blockcore.Networks.Network network, UIServices uiServices)
    {
        this.network = network;
        Back = back;
        currentHeader = Header(content).ToProperty(this, vm => vm.Content);
        this.WhenAnyValue(model => model.IsDarkThemeEnabled)
            .BindTo(uiServices, services => services.IsDarkThemeEnabled);
    }

    public IEnhancedCommand Back { get; }
    public object? Content => currentHeader.Value;

    private IObservable<object?> Header(object source)
    {
        if (source is IHaveHeader headered)
        {
            return Observable.Return(headered.Header);
        }
        
        var navigator = (source as Visual)?.FindLogicalDescendantOfType<WizardNavigator>();
        if (navigator != null)
        {
            return WizardHeader(navigator.Wizard);
        }

        return Observable.Return(new NetworkViewModel(network));
    }

    private static IObservable<object?> WizardHeader(ISlimWizard wizard)
    {
        return wizard
            .WhenAnyValue(x => x.CurrentPage.Content)
            .Select(step => Maybe<IHaveHeader>.From(step as IHaveHeader)
                .Map(h => h.Header)
                .GetValueOrDefault());
    }

    [Reactive] private bool isDarkThemeEnabled;
}