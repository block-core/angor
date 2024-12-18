using System.Threading.Tasks;
using System.Windows.Input;
using AngorApp.Common;
using AngorApp.Sections.Browse.Details.Invest.Amount;
using AngorApp.Sections.Browse.Details.Invest.TransactionPreview;
using AngorApp.Sections.Wallet;
using AngorApp.Services;
using CSharpFunctionalExtensions;
using Zafiro.Avalonia.Controls.Wizards.Builder;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.Sections.Browse.Details;

public class ProjectDetailsViewModel(Func<Maybe<IWallet>> getWallet, IProject project, UIServices uiServices) : ReactiveObject, IProjectDetailsViewModel
{
    public string Name => project.Name;
    public string ShortDescription => project.ShortDescription;
    public object Icon => project.Icon;
    public object Picture => project.Picture;

    public ICommand Invest { get; } = ReactiveCommand.CreateFromTask(() =>
    {
        var maybeWallet = getWallet();
        return maybeWallet.Match(wallet => DoInvest(wallet, project, uiServices), () => uiServices.NotificationService.Show("You need to create a Wallet before investing", "No wallet"));
    });

    public string NpubKey { get; } = "npub109t62lkxkfs7m4cac0lp0en45ndl3kdcnqm0serd450dravj9lvq3duh5k";
    public string NpubKeyHex { get; } = "7957a57ec6b261edd71dc3fe17e675a4dbf8d9b89836f8646dad1ed1f5922fd8";

    public IEnumerable<INostrRelay> Relays { get; } =
    [
        new NostrRelayDesign
        {
            Uri = new Uri("wss://relay.angor.io")
        },
        new NostrRelayDesign
        {
            Uri = new Uri("wss://relay2.angor.io")
        }
    ];

    public double TotalDays { get; } = 119;
    public double TotalInvestment { get; } = 1.5d;
    public double CurrentDays { get; } = 11;
    public double CurrentInvestment { get; } = 0.79d;
    public IProject Project => project;

    private static async Task DoInvest(IWallet wallet, IProject project, UIServices uiServices)
    {
        var wizard = WizardBuilder.StartWith(() => new AmountViewModel(wallet, project))
            .Then(viewModel => new TransactionPreviewViewModel(wallet, project, uiServices, viewModel.Amount!.Value))
            .Then(prev => new SuccessViewModel("Transaction confirmed!"))
            .Build();

        await uiServices.Dialog.Show(wizard,  @$"Invest in ""{project}""", closeable => wizard.OptionsForCloseable(closeable));
    }
}