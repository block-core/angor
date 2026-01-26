using Angor.Sdk.Funding.Founder;

namespace AngorApp.UI.Sections.Founder.ProjectDetails.MainView.Approve;

public class ApproveInvestmentsViewModelSample : IApproveInvestmentsViewModel
{
    public IEnumerable<IInvestmentViewModel> Investments { get; set; } =
    [
        new InvestmentViewModelSample()
        {
            MostRecentInvestment = { Status = InvestmentStatus.Invested, }
        },
        new InvestmentViewModelSample()
        {
            MostRecentInvestment = { Status = InvestmentStatus.PendingFounderSignatures, }
        },
        new InvestmentViewModelSample()
        {
            MostRecentInvestment = { Status = InvestmentStatus.Invested, }
        },
        new InvestmentViewModelSample()
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