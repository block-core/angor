using System.Linq;
using System.Reactive.Disposables;
using Angor.Contexts.Funding.Founder;
using Angor.Contexts.Funding.Founder.Operations;
using AngorApp.UI.Services;
using ReactiveUI;
using Zafiro.Avalonia.Dialogs;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using ProjectId = Angor.Contexts.Funding.Shared.ProjectId;

namespace AngorApp.Sections.Founder.ProjectDetails.MainView.Approve;

public partial class ApproveInvestmentsViewModel : ReactiveObject, IApproveInvestmentsViewModel
{
    private readonly IFounderAppService founderAppService;
    private readonly UIServices uiServices;
    private readonly IWalletContext walletContext;
    private readonly CompositeDisposable disposable = new();
    [ObservableAsProperty] private IEnumerable<IInvestmentViewModel> investments;

    public ApproveInvestmentsViewModel(IFullProject project, IFounderAppService founderAppService, UIServices uiServices, IWalletContext walletContext)
    {
        this.founderAppService = founderAppService;
        this.uiServices = uiServices;
        this.walletContext = walletContext;
        LoadInvestments = ReactiveCommand.CreateFromTask(() => GetInvestments(project)).DisposeWith(disposable);
        LoadInvestments.HandleErrorsWith(uiServices.NotificationService, "Could not load the investments").DisposeWith(disposable);
        investmentsHelper = LoadInvestments.Successes().ToProperty(this, x => x.Investments);

        LoadInvestments.Execute().Subscribe().DisposeWith(disposable);
    }

    private Task<Result<IEnumerable<IInvestmentViewModel>>> GetInvestments(IFullProject projectId)
    {
        return walletContext.RequiresWallet(w => GetWalletInvestments(projectId, w)
            .Map(tuple => tuple.investments.GroupBy(investment => new InvestmentGroupKey(investment, tuple.wallet))
                .Select(IInvestmentViewModel (grouping) => new InvestmentViewModel(grouping, () => Approve(projectId, tuple.wallet, grouping.Key.Investment)))));
    }

    private Task<Result<(IWallet wallet, IEnumerable<Investment> investments)>> GetWalletInvestments(IFullProject project, IWallet wallet)
    {
        var investments = founderAppService.GetInvestments(wallet.Id.Value, project.ProjectId);
        return investments.Map(x => (wallet, investments: x));
    }

    private async Task<bool> Approve(IFullProject project, IWallet wallet, Investment investment)
    {
        var confirmationResult = await uiServices.Dialog.ShowConfirmation("Approve investment", "Do you want to approve this investment?");

        if (confirmationResult.HasNoValue || !confirmationResult.Value)
        {
            return false;
        }

        var approvalResult = await founderAppService.ApproveInvestment(wallet.Id.Value, project.ProjectId, investment);

        return approvalResult.IsSuccess;
    }

    public ReactiveCommand<Unit, Result<IEnumerable<IInvestmentViewModel>>> LoadInvestments { get; }

    public void Dispose()
    {
        disposable.Dispose();
    }
}
