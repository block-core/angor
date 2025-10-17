using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;
using AngorApp.Sections.Wallet.CreateAndImport.Steps.CreateWelcome;
using AngorApp.Sections.Wallet.CreateAndImport.Steps.EncryptionPassword;
using AngorApp.Sections.Wallet.CreateAndImport.Steps.Passphrase.Create;
using AngorApp.Sections.Wallet.CreateAndImport.Steps.SeedWordsConfirmation;
using AngorApp.Sections.Wallet.CreateAndImport.Steps.SeedWordsGeneration;
using AngorApp.Sections.Wallet.CreateAndImport.Steps.Summary;
using AngorApp.UI.Controls.Common.Success;
using Zafiro.Avalonia.Dialogs.Wizards.Slim;
using Zafiro.UI.Wizards.Slim.Builder;

namespace AngorApp.Sections.Wallet.CreateAndImport;

public class WalletCreationWizard(UIServices uiServices, IWalletProvider walletProvider, IWalletAppService walletAppService, Func<BitcoinNetwork> getNetwork, IWalletContext walletContext)
{
    public async Task<Maybe<Unit>> Start()
    {
        SeedWords seedwords = null!;
        Maybe<string> passphrase = null;
        string encryptionKey = null!;

        var wizard = WizardBuilder
            .StartWith(() => new WelcomeViewModel(), "Create New Wallet").Next(_ => Unit.Default).Always()
            .Then(_ => new SeedWordsViewModel(walletAppService, uiServices), "Seed Words").NextResult(model => Result.Success(model.Words.Value!).Tap(x => seedwords = x)).WhenValid<SeedWordsViewModel>()
            .Then(_ => new SeedWordsConfirmationViewModel(seedwords), "Confirm Seed Words").Next(model => model.SeedWords).WhenValid<SeedWordsConfirmationViewModel>()
            .Then(_ => new PassphraseCreateViewModel(), "Passphrase").NextResult(model => Result.Success(model.Passphrase!).Tap(x => passphrase = x)).WhenValid<PassphraseCreateViewModel>()
            .Then(_ => new EncryptionPasswordViewModel(), "Encryption Key").NextResult(model => Result.Success(model.EncryptionKey!).Tap(x => encryptionKey = x)).WhenValid<EncryptionPasswordViewModel>()
            .Then(_ => new SummaryViewModel(walletAppService,
                walletProvider, uiServices,
                walletContext,
                new WalletImportOptions(
                    seedwords,
                    passphrase,
                    encryptionKey), getNetwork)
            {
                IsRecovery = false
            }, "Summary").NextCommand(model => model.CreateWallet.Enhance("Create Wallet"))
            .Then(_ => new SuccessViewModel("Wallet created successfully"), "Wallet Creation").Next(_ => Unit.Default, "Close").Always()
            .WithCompletionFinalStep();

        return await uiServices.Dialog.ShowWizard(wizard, "Create wallet").Map(_ => Unit.Default);
    }
}