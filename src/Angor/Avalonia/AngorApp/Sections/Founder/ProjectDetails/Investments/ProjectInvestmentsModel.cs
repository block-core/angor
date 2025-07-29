using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Angor.Contexts.Funding.Founder.Operations;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.Avalonia.Dialogs;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;

namespace AngorApp.Sections.Founder.ProjectDetails.Investments;

public partial class ProjectInvestmentsViewModel : ReactiveObject, IProjectInvestmentsViewModel
{
    private readonly CompositeDisposable disposable = new();
    [ObservableAsProperty] private IEnumerable<IInvestmentViewModel> investments;

    public ProjectInvestmentsViewModel(ProjectDto project, IInvestmentAppService investmentAppService, UIServices uiServices)
    {
        LoadInvestments = ReactiveCommand.CreateFromTask(() =>
            {
                return uiServices.WalletRoot.GetDefaultWalletAndActivate()
                    .Bind(maybe => maybe.ToResult("You need to create a wallet first"))
                    .Bind(wallet => GetWalletInvestments(project, investmentAppService, wallet))
                    .Map(tuple => tuple.investments.GroupBy(investment => new InvestmentGroupKey(investment, tuple.wallet))
                        .Select(IInvestmentViewModel (grouping) => new InvestmentViewModel(grouping, () => Approve(project, investmentAppService, uiServices, tuple.wallet, grouping.Key.Investment))));
            })
            .DisposeWith(disposable);

        LoadInvestments.HandleErrorsWith(uiServices.NotificationService, "Could not load the investments").DisposeWith(disposable);
        investmentsHelper = LoadInvestments.Successes().ToProperty(this, x => x.Investments);
        
        LoadInvestments.Execute().Subscribe().DisposeWith(disposable);
    }

    private static Task<Result<(IWallet wallet, IEnumerable<Investment> investments)>> GetWalletInvestments(ProjectDto project, IInvestmentAppService investmentAppService, IWallet wallet)
    {
        var investments = investmentAppService.GetInvestments(wallet.Id.Value, project.Id);
        return investments.Map(x => (wallet, investments: x));
    }

    private static async Task<bool> Approve(ProjectDto project, IInvestmentAppService investmentAppService, UIServices uiServices, IWallet wallet, Investment investment)
    {
        var confirmationResult = await uiServices.Dialog.ShowConfirmation("Approve investment", "Do you want to approve this investment?");
    
        if (confirmationResult.HasNoValue || confirmationResult.Value == false)
        {
            return false;
        }
    
        var approvalResult = await investmentAppService.ApproveInvestment(wallet.Id.Value, project.Id, investment);
    
        return approvalResult.IsSuccess;

    }
    
    public ReactiveCommand<Unit, Result<IEnumerable<IInvestmentViewModel>>> LoadInvestments { get; }

    public void Dispose()
    {
        disposable.Dispose();
    }
}