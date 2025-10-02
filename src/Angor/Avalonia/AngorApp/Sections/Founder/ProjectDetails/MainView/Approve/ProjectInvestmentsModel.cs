using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Angor.Contexts.Funding.Founder.Operations;
using Angor.Contexts.Funding.Investor;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.Avalonia.Dialogs;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;
using ProjectId = Angor.Contexts.Funding.Shared.ProjectId;

namespace AngorApp.Sections.Founder.ProjectDetails.MainView.Approve;

public partial class ApproveInvestmentsViewModel : ReactiveObject, IApproveInvestmentsViewModel
{
    private readonly CompositeDisposable disposable = new();
    [ObservableAsProperty] private IEnumerable<IInvestmentViewModel> investments;

    public ApproveInvestmentsViewModel(ProjectId projectId, IInvestmentAppService investmentAppService, UIServices uiServices)
    {
        LoadInvestments = ReactiveCommand.CreateFromTask(() =>
            {
                return uiServices.WalletRoot.GetDefaultWalletAndActivate()
                    .Bind(maybe => maybe.ToResult("You need to create a wallet first"))
                    .Bind(wallet => GetWalletInvestments(projectId, investmentAppService, wallet))
                    .Map(tuple => tuple.investments.GroupBy(investment => new InvestmentGroupKey(investment, tuple.wallet))
                        .Select(IInvestmentViewModel (grouping) => new InvestmentViewModel(grouping, () => Approve(projectId, investmentAppService, uiServices, tuple.wallet, grouping.Key.Investment))));
            })
            .DisposeWith(disposable);

        LoadInvestments.HandleErrorsWith(uiServices.NotificationService, "Could not load the investments").DisposeWith(disposable);
        investmentsHelper = LoadInvestments.Successes().ToProperty(this, x => x.Investments);
        
        LoadInvestments.Execute().Subscribe().DisposeWith(disposable);
    }

    private static Task<Result<(IWallet wallet, IEnumerable<Investment> investments)>> GetWalletInvestments(ProjectId projectId, IInvestmentAppService investmentAppService, IWallet wallet)
    {
        var investments = investmentAppService.GetInvestments(wallet.Id.Value, projectId);
        return investments.Map(x => (wallet, investments: x));
    }

    private static async Task<bool> Approve(ProjectId projectId, IInvestmentAppService investmentAppService, UIServices uiServices, IWallet wallet, Investment investment)
    {
        var confirmationResult = await uiServices.Dialog.ShowConfirmation("Approve investment", "Do you want to approve this investment?");
    
        if (confirmationResult.HasNoValue || confirmationResult.Value == false)
        {
            return false;
        }
    
        var approvalResult = await investmentAppService.ApproveInvestment(wallet.Id.Value, projectId, investment);
    
        return approvalResult.IsSuccess;

    }
    
    public ReactiveCommand<Unit, Result<IEnumerable<IInvestmentViewModel>>> LoadInvestments { get; }

    public void Dispose()
    {
        disposable.Dispose();
    }
}