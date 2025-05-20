using System.Threading.Tasks;
using Angor.Contexts.Funding.Founder.Operations;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using AngorApp.UI.Services;
using DynamicData;
using Zafiro;
using Zafiro.Avalonia.Dialogs;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;

namespace AngorApp.Sections.Founder.Details;

public class FounderProjectDetailsViewModel : IFounderProjectDetailsViewModel
{
    private readonly ProjectDto project;

    public FounderProjectDetailsViewModel(ProjectDto project, IInvestmentAppService investmentAppService, UIServices uiServices)
    {
        this.project = project;
        LoadInvestments = ReactiveCommand.CreateFromTask(() =>
        {
            return uiServices.WalletRoot.GetDefaultWalletAndActivate()
                .Bind(maybe => maybe.ToResult("You need to create a wallet first"))
                .Bind(wallet => investmentAppService.GetInvestments(wallet.Id.Value, project.Id)
                    .MapEach(IInvestmentViewModel (investment) => new InvestmentViewModel(investment, () => Approve(project, investmentAppService, uiServices, wallet, investment))));
        });

        LoadInvestments.HandleErrorsWith(uiServices.NotificationService, "Failed to get pending investments");

        LoadInvestments.Successes()
            .EditDiff(x => x, new LambdaComparer<IInvestmentViewModel>((a, b) => a.InvestorNostrPubKey == b.InvestorNostrPubKey))
            .TransformWithInlineUpdate(x => new IdentityContainer<IInvestmentViewModel> { Content = x }, (container, model) => container.Content = model)
            .Bind(out var pendingInvestments)
            .Subscribe();

        Investments = pendingInvestments;
    }

    private static Task<Maybe<Result<bool>>> Approve(ProjectDto project, IInvestmentAppService investmentAppService, UIServices uiServices, IWallet wallet, GetInvestments.Investment investment)
    {
        return uiServices.Dialog
            .ShowConfirmation("Approve investment", "Do you want to approve this investment?")
            .Map(isConfirmed => isConfirmed ? investmentAppService.ApproveInvestment(wallet.Id.Value, project.Id, investment).Map(() => true) : Task.FromResult(Result.Success(false)));
    }

    public IEnumerable<IdentityContainer<IInvestmentViewModel>> Investments { get; }
    public ReactiveCommand<Unit, Result<IEnumerable<IInvestmentViewModel>>> LoadInvestments { get; }
    public Uri? BannerUrl => project.Banner;
    public string ShortDescription => project.ShortDescription;
    public string Name => project.Name;
}