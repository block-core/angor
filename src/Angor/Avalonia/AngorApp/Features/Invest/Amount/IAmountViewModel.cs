using System.Linq;
using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.Features.Invest.Amount;

public interface IAmountViewModel : IStep
{
    public long? Amount { get; set; }
    IProject Project { get; }
    IEnumerable<Breakdown> StageBreakdowns { get; }
}