using Zafiro.UI;

namespace AngorApp.Sections.Portfolio.Manage;

public interface IInvestorProjectItem
{
    int Stage { get; }
    IAmountUI Amount { get; }
    string Status { get; }
}
