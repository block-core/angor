using AngorApp.UI.Shared.Controls.Feerate;

namespace AngorApp.UI.Shared.Controls;

public class DesignTimePreset : IFeeratePreset
{
    public IAmountUI Feerate { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
}