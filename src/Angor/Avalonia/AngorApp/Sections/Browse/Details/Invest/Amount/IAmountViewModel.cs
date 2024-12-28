using AngorApp.Model;
using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.Sections.Browse.Details.Invest.Amount;

public interface IAmountViewModel : IStep
{
    public ulong? Amount { get; set; }
    IProject Project { get; }
}