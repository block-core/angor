using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;
using AngorApp.Sections.Wallet.CreateAndImport.Steps.EncryptionPassword;
using AngorApp.Sections.Wallet.CreateAndImport.Steps.ImportWelcome;
using AngorApp.Sections.Wallet.CreateAndImport.Steps.Passphrase.Create;
using AngorApp.Sections.Wallet.CreateAndImport.Steps.RecoverySeedWords;
using AngorApp.Sections.Wallet.CreateAndImport.Steps.Summary;
using AngorApp.UI.Controls.Common.Success;
using ReactiveUI.Validation.Extensions;
using Zafiro.Avalonia.Dialogs.Wizards.Slim;
using Zafiro.UI.Wizards.Slim.Builder;

namespace AngorApp.Sections.Wallet.CreateAndImport;

public class WalletImportWizard(UIServices uiServices, IWalletProvider walletProvider, IWalletAppService walletAppService, Func<BitcoinNetwork> getNetwork, IWalletContext walletContext)
{
    public async Task<Maybe<Unit>> Start()
    {
        SeedWords seedWords = null!;
        Maybe<string> passphrase = null;
        string encryptionKey = null!;

        var wizard = WizardBuilder
            .StartWith(() => new ImportWelcomeViewModel(), _ => ReactiveCommand.Create(() => Result.Success(Unit.Default)).Enhance("Next"), "Wallet Recovery")
            .Then(_ => new RecoverySeedWordsViewModel(), model => ReactiveCommand.Create(() => Result.Success(model.SeedWords).Tap(x => seedWords = x), model.IsValid).Enhance("Next"), "Seed Words")
            .Then(_ => new PassphraseCreateViewModel(), model => ReactiveCommand.Create<Result<string>>(() => Result.Success<string>(model.Passphrase).Tap(x => passphrase = x), model.IsValid()).Enhance("Next"), "Passphrase")
            .Then(_ => new EncryptionPasswordViewModel(), model => ReactiveCommand.Create<Result<string>>(() => Result.Success<string>(model.EncryptionKey!).Tap(x => encryptionKey = x), model.IsValid()).Enhance("Next"), "Encryption Key")
            .Then(_ => new SummaryViewModel(walletAppService,
                walletProvider, uiServices, walletContext,
                new WalletImportOptions(
                    seedWords,
                    passphrase,
                    encryptionKey), getNetwork)
            {
                IsRecovery = true
            }, model => model.CreateWallet.Enhance("Import Wallet"), "Summary")
            .Then(_ => new SuccessViewModel("Wallet imported successfully"), model => ReactiveCommand.Create(() => Result.Success(Unit.Default)).Enhance("Close"), "Wallet Recovery")
            .WithCompletionFinalStep();

        return await uiServices.Dialog.ShowWizard(wizard, "Recover wallet").Map(_ => Unit.Default);
    }
}