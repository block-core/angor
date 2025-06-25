using AngorApp.UI.Controls;

namespace AngorApp.Features.Invest.Draft;

public interface IDraftViewModel
{
    public long? Feerate { get; set; }
    public IAmountUI? Fee { get; }
    public IEnumerable<IFeeratePreset> Presets { get; }
    public IObservable<bool> IsCalculatingDraft { get; }
    public IObservable<bool> IsSending { get; }
    IProject Project { get; }
    IInvestmentDraft? Draft { get; }
    public IAmountUI AmountToOffer { get; }
}