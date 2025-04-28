using AngorApp.UI.Controls;
using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.Features.Invest.Draft;

public interface IDraftViewModel : IStep
{
    public long SatsToInvest { get; }
    InvestmentDraft Draft { get; }
    public long? Feerate { get; set; }
    public IFeeCalculator FeeCalculator { get; }
    public IEnumerable<IFeeratePreset> Presets { get; }
}