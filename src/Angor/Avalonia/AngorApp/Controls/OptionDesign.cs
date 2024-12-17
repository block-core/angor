using System.Reactive.Linq;
using Zafiro.Avalonia.Commands;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.Controls;

public class OptionDesign : IOption
{
    public string Title { get; set; }
    public IEnhancedCommand Command { get; }
    public bool IsDefault { get; set; }
    public bool IsCancel { get; set; }
    public IObservable<bool> IsVisible { get; } = Observable.Return(true);
}