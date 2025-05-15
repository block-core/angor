using Angor.Contexts.Funding.Founder.Operations;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using AngorApp.UI.Services;
using DynamicData;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;

namespace AngorApp.Sections.Founder.Details;

public class FounderProjectDetailsViewModel : IFounderProjectDetailsViewModel
{
    private readonly ProjectDto projectDto;

    public FounderProjectDetailsViewModel(ProjectDto projectDto, IInvestmentAppService investmentAppService, UIServices uiServices)
    {
        this.projectDto = projectDto;
        LoadPendingInvestments = ReactiveCommand.CreateFromTask(token =>
        {
            return uiServices.WalletRoot.GetDefaultWalletAndActivate()
                .Bind(maybe => maybe.ToResult("You need to create a wallet first"))
                .Bind(wallet => investmentAppService.GetPendingInvestments(wallet.Id.Value, projectDto.Id));
        });

        LoadPendingInvestments.HandleErrorsWith(uiServices.NotificationService, "Failed to get pending investments");
        
        LoadPendingInvestments.Successes()
            .EditDiff(dto => dto)
            .Bind(out var pendingInvestments)
            .Subscribe();
        
        PendingInvestments = pendingInvestments;
    }

    public IEnumerable<GetPendingInvestments.PendingInvestmentDto> PendingInvestments { get; }

    public ReactiveCommand<Unit, Result<IEnumerable<GetPendingInvestments.PendingInvestmentDto>>> LoadPendingInvestments { get; }
    public Uri? BannerUrl => projectDto.Banner;
    public string ShortDescription => projectDto.ShortDescription;
    public string Name => projectDto.Name;
}