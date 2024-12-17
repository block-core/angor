using Zafiro.UI;

namespace AngorApp.Sections.Browse.Details.Invest.Amount;

public interface IAmountViewModel : IValidatable
{
    public decimal? Amount { get; set; }
}