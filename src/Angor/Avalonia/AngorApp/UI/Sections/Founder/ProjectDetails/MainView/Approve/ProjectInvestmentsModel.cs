using System.Linq;
using System.Reactive.Disposables;
using Angor.Contexts.Funding.Founder;
using Angor.Contexts.Funding.Founder.Domain;
using Angor.Contexts.Funding.Founder.Operations;
using AngorApp.UI.Shared.Services;
using ReactiveUI;
using Zafiro.Avalonia.Dialogs;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using ProjectId = Angor.Contexts.Funding.Shared.ProjectId;

namespace AngorApp.UI.Sections.Founder.ProjectDetails.MainView.Approve;

public partial class ApproveInvestmentsViewModel : ReactiveObject, IApproveInvestmentsViewModel
{
    private readonly IFounderAppService founderAppService;
    private readonly UIServices uiServices;
    private readonly IWalletContext walletContext;
    private readonly CompositeDisposable disposable = new();
    [ObservableAsProperty] private IEnumerable<IInvestmentViewModel> investments;

    public ApproveInvestmentsViewModel(ProjectId projectId, IFounderAppService founderAppService, UIServices uiServices, IWalletContext walletContext)
    {
        this.founderAppService = founderAppService;
        this.uiServices = uiServices;
        this.walletContext = walletContext;
        LoadInvestments = ReactiveCommand.CreateFromTask(() => GetInvestments(projectId)).DisposeWith(disposable);
        LoadInvestments.HandleErrorsWith(uiServices.NotificationService, "Could not load the investments").DisposeWith(disposable);
        investmentsHelper = LoadInvestments.Successes().ToProperty(this, x => x.Investments);
    }

    private Task<Result<IEnumerable<IInvestmentViewModel>>> GetInvestments(ProjectId projectId)
    {
        return walletContext.RequiresWallet(w => GetWalletInvestments(projectId, w)
            .Map(tuple => tuple.investments.GroupBy(investment => new InvestmentGroupKey(investment, tuple.wallet))
                .Select(IInvestmentViewModel (grouping) => new InvestmentViewModel(grouping, () => Approve(projectId, tuple.wallet, grouping.Key.Investment)))));
    }

    private Task<Result<(IWallet wallet, IEnumerable<Investment> investments)>> GetWalletInvestments(ProjectId projectId, IWallet wallet)
    {
        var investments = founderAppService.GetInvestments(wallet.Id, projectId);
        return investments.Map(x => (wallet, investments: x));
    }

    private async Task<bool> Approve(ProjectId projectId, IWallet wallet, Investment investment)
    {
        var confirmationResult = await uiServices.Dialog.ShowConfirmation("Approve investment", "Do you want to approve this investment?");

        if (confirmationResult.HasNoValue || !confirmationResult.Value)
        {
            return false;
        }

        var approvalResult = await founderAppService.ApproveInvestment(wallet.Id, projectId, investment);

        return approvalResult.IsSuccess;
    }

    public ReactiveCommand<Unit, Result<IEnumerable<IInvestmentViewModel>>> LoadInvestments { get; }

    public void Dispose()
    {
        disposable.Dispose();
    }
}
