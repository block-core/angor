using AngorApp.UI.Controls;

namespace AngorApp.Features.Invest.Draft;

public interface IDraftViewModel
{
    public long? Feerate { get; set; }
    public IAmountUI? Fee { get; }
    public IEnumerable<IFeeratePreset> Presets { get; }
    public IObservable<bool> IsCalculating { get; }
    public IObservable<bool> IsSending { get; }
}