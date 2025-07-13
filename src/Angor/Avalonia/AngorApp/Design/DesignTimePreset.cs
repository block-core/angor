using AngorApp.UI.Controls;

namespace AngorApp;

public class DesignTimePreset : IFeeratePreset
{
    public IAmountUI Feerate { get; set; }
    public string Name { get; set; }
}