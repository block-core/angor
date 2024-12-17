using Zafiro.Avalonia.Commands;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp;

public class OptionDesign : IOption
{
    public string Title { get; set; }
    public IEnhancedCommand Command { get; }
    public bool IsDefault { get; set; }
    public bool IsCancel { get; set; }
}