using System.Linq;
using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.Features.Invest.Amount;

public interface IAmountViewModel : IStep
{
    public long? Amount { get; set; }
    IProject Project { get; }
    IEnumerable<StageBreakdown> StageBreakdowns { get; }
}

public record StageBreakdown(
    int Index,
    long Amount,
    double Weight,
    DateTime ReleaseDate)
{
    public long InvestmentSats => (long)(Amount * Weight);

    public string Description =>
        $"Stage {Index}: invest {InvestmentSats} sats that will be released on {ReleaseDate:d}";
}
