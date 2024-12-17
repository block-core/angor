using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.Sections.Browse.Details.Invest.Amount;

public interface IAmountViewModel : IStep
{
    public decimal? Amount { get; set; }
}