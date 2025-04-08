using AngorApp.Features.Invest.Draft;
using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.Sections.Browse.Details.Invest.Draft;

public interface IDraftViewModel : IStep
{
    public long SatsToInvest { get; }
    InvestmentDraft Draft { get; }
    public long Feerate { get; set; }
}