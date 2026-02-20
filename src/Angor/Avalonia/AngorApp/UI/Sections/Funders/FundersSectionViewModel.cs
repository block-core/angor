using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Founder.Domain;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Projects;
using DynamicData;
using Zafiro.UI.Shell.Utils;
using AngorApp.UI.Sections.Funders.Items;
using AngorApp.UI.Sections.Funders.Grouping;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Mixins;
using ProjectId = Angor.Sdk.Funding.Shared.ProjectId;

namespace AngorApp.UI.Sections.Funders;

[Section("Funders", "fa-user-group", 5)]
[SectionGroup("FOUNDER")]
public class FundersSectionViewModel : IFundersSectionViewModel
{
    private readonly UIServices uiServices;
    private readonly IFounderAppService founderAppService;
    private readonly IProjectAppService projectAppService;
    private readonly IWalletContext walletContext;

    public FundersSectionViewModel(
        UIServices uiServices,
        IFounderAppService founderAppService,
        IProjectAppService projectAppService,
        IWalletContext walletContext
    )
    {
        this.uiServices = uiServices;
        this.founderAppService = founderAppService;
        this.projectAppService = projectAppService;
        this.walletContext = walletContext;

        RefreshableCollection<IFunderItem, string> funders = RefreshableCollection.Create(GetItems, GetItemKey);

        Groups =
        [
            new FunderGroup("Pending", funders.Changes.Filter(item => item.Status == FunderStatus.Pending)),
            new FunderGroup("Approved", funders.Changes.Filter(item => item.Status == FunderStatus.Approved)),
            new FunderGroup("Rejected", funders.Changes.Filter(item => item.Status == FunderStatus.Rejected))
        ];

        IsEmpty = funders.Changes.IsEmpty();
        Load = funders.Refresh;
    }

    public IEnumerable<IFunderGroup> Groups { get; }
    public IEnhancedCommand Load { get; }
    public IObservable<bool> IsEmpty { get; }

    private async Task<Result> ApproveInvestment(WalletId walletId, ProjectId projectId, Investment investment)
    {
        return await founderAppService
            .ApproveInvestment(new ApproveInvestment.ApproveInvestmentRequest(walletId, projectId, investment))
            .Tap(_ => Load.Execute(null));
    }

    private Task<Result> RejectInvestment(WalletId walletId, ProjectId projectId, Investment investment)
    {
        return Task.FromResult(Result.Failure("Reject investment is not supported by the SDK yet."));
    }

    private async Task<Result<IEnumerable<IFunderItem>>> GetItems()
    {
        var items = await walletContext.Require()
                     .Bind(wallet => projectAppService.GetFounderProjects(wallet.Id)
                                                      .Map(response =>
                                                               response.Projects.Select(prj => new { wallet, prj })))
                     .MapSequentially(input => founderAppService.GetProjectInvestments(
                                          new GetProjectInvestments.GetProjectInvestmentsRequest(
                                              input.wallet.Id,
                                              input.prj.Id)).Map(response => new { input.wallet, input.prj, response }))
                     .MapEach(arg => arg.response.Investments.Where(IsCandidate).Select(IFunderItem (investment) => new FunderItem(
                         uiServices,
                         () => ApproveInvestment(arg.wallet.Id, arg.prj.Id, investment),
                         () => RejectInvestment(arg.wallet.Id, arg.prj.Id, investment))
                     {
                         ProjectId = arg.prj.Id,
                         Amount = new AmountUI(investment.Amount),
                         DateCreated = investment.CreatedOn,
                         InvestorNpub =
                             investment.InvestorNostrPubKey,
                         Name = arg.prj.Name,
                         Status = GetStatus(investment.Status)
                     }))
                     .Map(items => items.Flatten());

        return items;
    }

    private static bool IsCandidate(Investment investment)
    {
        return investment.Status == InvestmentStatus.PendingFounderSignatures || 
               investment.Status == InvestmentStatus.FounderSignaturesReceived ||
               investment.Status == InvestmentStatus.Cancelled;
    }

    private static string GetItemKey(IFunderItem item)
    {
        return $"{item.ProjectId.Value}::{item.InvestorNpub}";
    }

    private FunderStatus GetStatus(InvestmentStatus investmentStatus)
    {
        return investmentStatus switch
        {
            InvestmentStatus.PendingFounderSignatures => FunderStatus.Pending,
            InvestmentStatus.Cancelled => FunderStatus.Rejected,
            InvestmentStatus.FounderSignaturesReceived => FunderStatus.Approved,
            InvestmentStatus.Invested => FunderStatus.Invested,
            InvestmentStatus.Invalid => FunderStatus.None,
            _ => throw new ArgumentOutOfRangeException(nameof(investmentStatus), investmentStatus, null)
        };
    }
}
