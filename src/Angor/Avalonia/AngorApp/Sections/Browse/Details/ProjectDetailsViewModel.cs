using System.Threading.Tasks;
using System.Windows.Input;
using AngorApp.Sections.Browse.Details.Invest.Amount;
using AngorApp.Sections.Browse.Details.Invest.TransactionPreview;
using AngorApp.Sections.Wallet;
using AngorApp.Services;
using CSharpFunctionalExtensions;
using Zafiro.Avalonia.Commands;
using Zafiro.Avalonia.Controls.Wizards.Builder;
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

    public string NpubKey { get; }
    public string NpubKeyHex { get; }
    public IEnumerable<INostrRelay> Relays { get; }

    private static async Task DoInvest(IWallet wallet, Project project, UIServices uiServices)
    {
        var wizard = WizardBuilder.StartWith(() => new AmountViewModel(wallet, project, uiServices))
            .Then(viewModel => new TransactionPreviewViewModel(wallet, project, viewModel.Amount!.Value))
            .Build();

        Func<ICloseable, IOption[]> func = closeable =>
        [
            OptionBuilder.Create("Next", wizard.Next),
            OptionBuilder.Create("Previous", wizard.Back),
            OptionBuilder.Create("Close", EnhancedCommand.Create(ReactiveCommand.Create(closeable.Close, wizard.IsLastPage)))
        ];

        await uiServices.Dialog.Show(wizard, $"Invest in {project}", func);
    }
}