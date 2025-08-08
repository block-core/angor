using AngorApp.UI.Controls;
using AngorApp.UI.Controls.Feerate;

namespace AngorApp.Design;

public class DesignTimePreset : IFeeratePreset
{
    public IAmountUI Feerate { get; set; }
    public string Name { get; set; }
}