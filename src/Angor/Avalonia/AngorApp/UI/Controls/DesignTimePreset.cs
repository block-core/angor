using AngorApp.UI.Controls.Feerate;

namespace AngorApp.UI.Controls;

public class DesignTimePreset : IFeeratePreset
{
    public IAmountUI Feerate { get; set; }
    public string Name { get; set; }
}