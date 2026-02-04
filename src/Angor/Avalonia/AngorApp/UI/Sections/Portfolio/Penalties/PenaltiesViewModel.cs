using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Windows.Input;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using AngorApp.Model.Common;
using AngorApp.UI.Sections.Portfolio.Recover;
using DynamicData;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI;
using Zafiro.UI.Navigation;

namespace AngorApp.UI.Sections.Portfolio.Penalties;

public class PenaltiesViewModel : ReactiveObject, IPenaltiesViewModel, IDisposable
{
    private readonly CompositeDisposable disposable = new();
    private readonly RefreshableCollection<IPenaltyViewModel, string> penaltiesCollection;

    public PenaltiesViewModel(IInvestmentAppService investmentAppService, INavigator navigator, IWalletContext walletContext, UIServices uiServices)
    {
        GoToRecovery = ReactiveCommand.Create(() => navigator.Go<IRecoverViewModel>());

        penaltiesCollection = RefreshableCollection.Create(
                () => walletContext.Require().Bind(wallet => investmentAppService.GetPenalties(new GetPenalties.GetPenaltiesRequest(wallet.Id))
                    .Map(response => response.Penalties)
                    .MapEach(IPenaltyViewModel (dto) => new PenaltyViewModel(dto))),
                model => model.InvestorPubKey)
            .DisposeWith(disposable);

        Load = penaltiesCollection.Refresh;
        Load.HandleErrorsWith(uiServices.NotificationService, "Failed to load penalties").DisposeWith(disposable);

        Penalties = penaltiesCollection.Items;
    }

    public IEnhancedCommand<Result<IEnumerable<IPenaltyViewModel>>> Load { get; }

    public ICommand GoToRecovery { get; }
    public IReadOnlyCollection<IPenaltyViewModel> Penalties { get; }

    public void Dispose()
    {
        disposable.Dispose();
    }
}
