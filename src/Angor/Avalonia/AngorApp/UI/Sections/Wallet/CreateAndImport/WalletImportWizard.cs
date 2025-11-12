using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;
using AngorApp.UI.Sections.Wallet.CreateAndImport.Steps.EncryptionPassword;
using AngorApp.UI.Sections.Wallet.CreateAndImport.Steps.ImportWelcome;
using AngorApp.UI.Sections.Wallet.CreateAndImport.Steps.Passphrase.Create;
using AngorApp.UI.Sections.Wallet.CreateAndImport.Steps.RecoverySeedWords;
using AngorApp.UI.Sections.Wallet.CreateAndImport.Steps.Summary;
using AngorApp.UI.Shared.Controls.Common.Success;
using Zafiro.Avalonia.Dialogs.Wizards.Slim;
using Zafiro.UI.Wizards.Slim.Builder;

namespace AngorApp.UI.Sections.Wallet.CreateAndImport;

public class WalletImportWizard(UIServices uiServices, IWalletProvider walletProvider, IWalletAppService walletAppService, Func<BitcoinNetwork> getNetwork, IWalletContext walletContext)
{
    public async Task<Maybe<Unit>> Start()
    {
        SeedWords seedWords = null!;
        Maybe<string> passphrase = null;
        string encryptionKey = null!;

        var wizard = WizardBuilder
            .StartWith(() => new ImportWelcomeViewModel(), "Wallet Recovery").Next(_ => Unit.Default).Always()
            .Then(_ => new RecoverySeedWordsViewModel(), "Seed Words").NextResult(model => Result.Success(model.SeedWords).Tap(x => seedWords = x)).WhenValid<RecoverySeedWordsViewModel>()
            .Then(_ => new PassphraseCreateViewModel(), "Passphrase").NextResult(model => Result.Success(model.Passphrase!).Tap(x => passphrase = x)).WhenValid<PassphraseCreateViewModel>()
            .Then(_ => new EncryptionPasswordViewModel(), "Encryption Key").NextResult(model => Result.Success(model.EncryptionKey!).Tap(x => encryptionKey = x)).WhenValid<EncryptionPasswordViewModel>()
            .Then(_ => new SummaryViewModel(walletAppService,
                walletProvider, uiServices, walletContext,
                new WalletImportOptions(
                    seedWords,
                    passphrase,
                    encryptionKey), getNetwork)
            {
                IsRecovery = true
            }, "Summary").NextCommand(model => model.CreateWallet.Enhance("Import Wallet"))
            .Then(_ => new SuccessViewModel("Wallet imported successfully"), "Wallet Recovery").Next(_ => Unit.Default, "Close").Always()
            .WithCompletionFinalStep();

        return await uiServices.Dialog.ShowWizard(wizard, "Recover wallet").Map(_ => Unit.Default);
    }
}
