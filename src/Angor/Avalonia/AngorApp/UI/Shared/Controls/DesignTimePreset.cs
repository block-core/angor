using AngorApp.UI.Shared.Controls.Feerate;

namespace AngorApp.UI.Shared.Controls;

public class DesignTimePreset : IFeeratePreset
{
    public IAmountUI Feerate { get; set; }
    public string Name { get; set; }
}