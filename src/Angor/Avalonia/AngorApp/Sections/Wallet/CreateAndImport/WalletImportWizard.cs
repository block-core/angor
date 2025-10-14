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
            .StartWith(() => new ImportWelcomeViewModel(), "Wallet Recovery").NextAlways()
            .Then(_ => new RecoverySeedWordsViewModel(), "Seed Words").NextWhenValid(model => Result.Success(model.SeedWords).Tap(x => seedWords = x))
            .Then(_ => new PassphraseCreateViewModel(), "Passphrase").NextWhenValid(model => Result.Success(model.Passphrase!).Tap(x => passphrase = x))
            .Then(_ => new EncryptionPasswordViewModel(), "Encryption Key").NextWhenValid(model => Result.Success(model.EncryptionKey!).Tap(x => encryptionKey = x))
            .Then(_ => new SummaryViewModel(walletAppService,
                walletProvider, uiServices, walletContext,
                new WalletImportOptions(
                    seedWords,
                    passphrase,
                    encryptionKey), getNetwork)
            {
                IsRecovery = true
            }, "Summary").NextWith(model => model.CreateWallet.Enhance("Import Wallet"))
            .Then(_ => new SuccessViewModel("Wallet imported successfully"), "Wallet Recovery").NextAlways("Close")
            .WithCompletionFinalStep();

        return await uiServices.Dialog.ShowWizard(wizard, "Recover wallet").Map(_ => Unit.Default);
    }
}
