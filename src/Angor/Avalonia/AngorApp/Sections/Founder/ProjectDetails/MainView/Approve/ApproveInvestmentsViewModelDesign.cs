using Angor.Contexts.Funding.Founder;

namespace AngorApp.Sections.Founder.ProjectDetails.MainView.Approve;

public class ApproveInvestmentsViewModelDesign : IApproveInvestmentsViewModel
{
    public IEnumerable<IInvestmentViewModel> Investments { get; set; } =
    [
        new InvestmentViewModelDesign()
        {
            MostRecentInvestment = { Status = InvestmentStatus.Invested, }
        },
        new InvestmentViewModelDesign()
        {
            MostRecentInvestment = { Status = InvestmentStatus.PendingFounderSignatures, }
        },
        new InvestmentViewModelDesign()
        {
            MostRecentInvestment = { Status = InvestmentStatus.Invested, }
        },
        new InvestmentViewModelDesign()
        {
            MostRecentInvestment =
            {
                Status = InvestmentStatus.Invalid,
            },
            OtherInvestments = [],
        },
    ];

    public ReactiveCommand<Unit, Result<IEnumerable<IInvestmentViewModel>>> LoadInvestments { get; }
    public bool IsProjectStarted { get; } = true;

    public void Dispose()
    {
        LoadInvestments.Dispose();
    }
}