using System.Windows.Input;
using Angor.Contexts.Funding.Investor;
using AngorApp.Sections.Portfolio.Recover;
using AngorApp.UI.Services;
using DynamicData;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Commands;
using Zafiro.UI.Navigation;

namespace AngorApp.Sections.Portfolio.Penalties;

public class PenaltiesViewModel : ReactiveObject, IPenaltiesViewModel
{
    public PenaltiesViewModel(IInvestmentAppService investmentAppService, UIServices uiServices, INavigator navigator)
    {
        GoToRecovery = ReactiveCommand.Create(() => navigator.Go<IRecoverViewModel>());
        Load = ReactiveCommand.CreateFromTask(() =>
        {
            return uiServices.WalletRoot.GetDefaultWalletAndActivate()
                .Bind(maybe => maybe.ToResult("You need to create a wallet first."))
                .Bind(wallet => investmentAppService.GetPenalties(wallet.Id.Value))
                .MapEach(IPenaltyViewModel (dto) => new PenaltyViewModel(dto));
        }).Enhance();

        Load.Successes()
            .EditDiff(model => model.InvestorPubKey)
            .Bind(out var penalties)
            .Subscribe();
        
        Penalties = penalties;

        // Automatically load penalties when the view model is created
        Load.Execute().Subscribe();
    }

    public EnhancedCommand<Result<IEnumerable<IPenaltyViewModel>>> Load { get; set; }

    public ICommand GoToRecovery { get; }
    public IEnumerable<IPenaltyViewModel> Penalties { get; }
}