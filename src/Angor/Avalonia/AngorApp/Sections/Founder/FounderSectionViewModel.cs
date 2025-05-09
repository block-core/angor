using Angor.Contexts.Funding.Founder.Operations;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Domain;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;

namespace AngorApp.Sections.Founder;

public partial class FounderSectionViewModel : ReactiveObject, IFounderSectionViewModel
{
    public FounderSectionViewModel(UIServices uiServices, IInvestmentAppService investmentAppService)
    {
        this.GetPendingInvestments = ReactiveCommand.CreateFromTask(() =>
        {
            return uiServices.WalletRoot.GetDefaultWalletAndActivate()
                .Bind(maybeWallet => maybeWallet
                    .ToResult("No default wallet")
                    .Bind(wallet => investmentAppService.GetPendingInvestments(wallet.Id.Value, new ProjectId("angor1qatlv9htzte8vtddgyxpgt78ruyzaj57n4l7k46"))));
        });
        
        GetPendingInvestments.HandleErrorsWith(uiServices.NotificationService, "Failed to get pending investments");
        pendingHelper = GetPendingInvestments.Successes().ToProperty(this, model => model.Pending);
    }

    public ReactiveCommand<Unit, Result<IEnumerable<GetPendingInvestments.PendingInvestmentDto>>> GetPendingInvestments { get; set; }

    [ObservableAsProperty]
    private IEnumerable<GetPendingInvestments.PendingInvestmentDto> pending;
}