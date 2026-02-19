using System.Reactive.Subjects;
using Angor.Sdk.Wallet.Application;
using AngorApp.UI.Flows.AddWallet.Import;
using AngorApp.UI.Flows.AddWallet.SeedBackup;
using AngorApp.UI.Shared.OperationResult;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Reactive;
using Option = Zafiro.Avalonia.Dialogs.Option;

namespace AngorApp.UI.Flows.AddWallet;

public class AddWalletFlow(UIServices uiServices, IWalletContext walletContext, IWalletAppService walletAppService, ISeedBackupFileService seedBackupFileService) : IAddWalletFlow
{
    public Task Run() => uiServices.Dialog.Show("", "Add New Wallet", closeable => [ImportOption(closeable), GenerateNewOption(closeable)]);

    private async Task Import()
    {
        SeedwordsEntryViewModel seedwordsEntryVm = new();
        var viewModel = new OperationResultViewModel(
            "Import Wallet",
            "Enter your BIP-39 seed words separated by spaces to import an external wallet.",
            additionalContent: seedwordsEntryVm);

        await uiServices.Dialog.Show(
            viewModel,
            "",
            closeable => ImportOptions(closeable, seedwordsEntryVm));
    }

    private async Task<Result> CreateWallet(string seedwords)
    {
        return await walletContext.ImportWallet(seedwords, Maybe.None);
    }

    private async Task CreateNew()
    {
        var seedwords = walletAppService.GenerateRandomSeedwords();
        var viewModel = new OperationResultViewModel(
            "Backup Your Wallet",
            "Download your seed phrase and keep it safe. You'll need this to recover your wallet if you lose access.",
            new Icon("fa-lock"),
            new SeedWordsViewModel(seedwords));

        await uiServices.Dialog.Show(
            viewModel,
            "",
            closeable => CreateNewOptions(closeable, seedwords));
    }

    private async Task NotifyError(string message)
    {
        await ShowDoneDialog(new OperationResultViewModel(
                                 "Could not create wallet",
                                 "An error occurred while creating your wallet: " + message + ".",
                                 new Icon("fa-xmark")) { Feeling = Feeling.Bad });
    }

    private async Task NotifySuccess()
    {
        await ShowDoneDialog(
            new OperationResultViewModel(
                "Wallet Created!",
                "Your new wallet has been successfully created. You can now use it to fund or launch projects",
                new Icon("fa-check")));
    }

    private IOption ImportOption(ICloseable closeable)
    {
        return new Option(
            "Import",
            EnhancedCommand.Create(async () =>
            {
                closeable.Close();
                await Import();
            }),
            new Settings { Role = OptionRole.Secondary });
    }

    private IOption GenerateNewOption(ICloseable closeable)
    {
        return new Option(
            "Generate New",
            EnhancedCommand.Create(async () =>
            {
                closeable.Close();
                await CreateNew();
            }),
            new Settings { Role = OptionRole.Primary, IsDefault = true });
    }

    private IOption[] ImportOptions(ICloseable closeable, SeedwordsEntryViewModel seedwordsEntryVm)
    {
        IEnhancedCommand<Unit> createWalletCommand = EnhancedCommand.Create(async () =>
        {
            await CreateWallet(seedwordsEntryVm.Seedwords!).Match(NotifySuccess, NotifyError);
            closeable.Close();
        }, seedwordsEntryVm.IsValid);

        return
        [
            CancelOption(closeable, createWalletCommand.IsExecuting.Not()),
            new Option("Continue", createWalletCommand, new Settings()),
        ];
    }

    private IOption[] CreateNewOptions(ICloseable closeable, string seedwords)
    {
        var downloadSeedExecuted = new BehaviorSubject<bool>(false);

        IEnhancedCommand<Unit> createWalletCommand = EnhancedCommand.Create(async () =>
        {
            closeable.Close();
            await CreateWallet(seedwords).Match(NotifySuccess, NotifyError);
        }, downloadSeedExecuted);

        return
        [
            DownloadSeedOption(seedwords, downloadSeedExecuted),
            new Option("Continue", createWalletCommand, new Settings { Role = OptionRole.Primary }),
            CancelOption(closeable, createWalletCommand.IsExecuting.Not()),
        ];
    }

    private Task ShowDoneDialog(OperationResultViewModel viewModel)
    {
        return uiServices.Dialog.Show(
            viewModel,
            "",
            closeable =>
            [
                new Option(
                    "Done",
                    EnhancedCommand.Create(closeable.Close),
                    new Settings { IsDefault = true, Role = OptionRole.Primary })
            ]);
    }

    private IOption CancelOption(ICloseable closeable, IObservable<bool> canCancel)
    {
        return new Option("Cancel", EnhancedCommand.Create(closeable.Close, canCancel), new Settings { IsCancel = true, Role = OptionRole.Cancel });
    }

    private IOption DownloadSeedOption(string seedwords, IObserver<bool> downloadSeedExecuted)
    {
        return new Option("Download Seed", EnhancedCommand.Create(async () =>
        {
            downloadSeedExecuted.OnNext(true);
            await seedBackupFileService.Save(seedwords);
        }), new Settings { Role = OptionRole.Info });
    }
}
