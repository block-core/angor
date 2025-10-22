using CSharpFunctionalExtensions;
using Zafiro.UI;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Portfolio.Manage;

public interface IInvestorProjectItem
{
    int Stage { get; }
    IAmountUI Amount { get; }
    string Status { get; }

    // Row actions
    IEnhancedCommand<Result> Recover { get; }
    IEnhancedCommand<Result> Release { get; }
    IEnhancedCommand<Result> ClaimEndOfProject { get; }
    IEnhancedCommand<Result> ClaimImmediate { get; }

    // Visibility helpers
    bool ShowRecover { get; }
    bool ShowRelease { get; }
    bool ShowClaimEndOfProject { get; }
    bool ShowClaimImmediate { get; }
}
