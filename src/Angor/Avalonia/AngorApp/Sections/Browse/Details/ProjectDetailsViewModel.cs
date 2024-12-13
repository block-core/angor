using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AngorApp.Sections.Browse.Details.Invest;
using AngorApp.Sections.Wallet;
using AngorApp.Services;
using CSharpFunctionalExtensions;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.Sections.Browse.Details;

public class ProjectDetailsViewModel(Func<Maybe<IWallet>> getWallet, Project project, UIServices uiServices) : ReactiveObject, IProjectDetailsViewModel
{
    public string Name => project.Name;
    public string ShortDescription => project.ShortDescription;
    public object Icon => project.Icon;
    public object Picture => project.Picture;
    public IEnumerable<Stage> Stages { get; }

    public ICommand Invest { get; } = ReactiveCommand.CreateFromTask(() =>
    {
        var maybeWallet = getWallet();
        return maybeWallet.Match(wallet => DoInvest(wallet, project, uiServices), () => uiServices.NotificationService.Show("You need to create a Wallet before investing", "No wallet"));
    });

    private static async Task DoInvest(IWallet getWallet, Project project, UIServices uiServices)
    {
        await uiServices.Dialog.Show(new NavigationViewModel(navigator => new InvestViewModel(getWallet, project, uiServices, navigator)), "Invest", closeable =>
        [
            new Option("Close", ReactiveCommand.Create(closeable.Close, Observable.Return(true)))
        ]);
    }
}