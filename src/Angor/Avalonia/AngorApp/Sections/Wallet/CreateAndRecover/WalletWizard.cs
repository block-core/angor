using System.Threading.Tasks;
using Angor.Wallet.Application;
using Angor.Wallet.Domain;
using AngorApp.Sections.Wallet.CreateAndRecover.Steps.Passphrase.Recover;
using AngorApp.Sections.Wallet.CreateAndRecover.Steps.RecoverySeedWords;
using AngorApp.Sections.Wallet.CreateAndRecover.Steps.RecoveryWelcome;
using AngorApp.Sections.Wallet.CreateAndRecover.Steps.SeedWordsConfirmation;
using AngorApp.UI.Controls.Common.Success;
using Zafiro.Avalonia.Controls.Wizards.Builder;
using Zafiro.Avalonia.Dialogs;
using SummaryViewModel = AngorApp.Sections.Wallet.CreateAndRecover.Steps.Summary.SummaryViewModel;

namespace AngorApp.Sections.Wallet.CreateAndRecover;

public class WalletWizard(UI.Services.UIServices uiServices, IWalletBuilder walletBuilder, IWalletAppService walletAppService, Func<BitcoinNetwork> getNetwork) : IWalletWizard
{
    public async Task<Maybe<Unit>> CreateNew()
    {
        SeedWords seedWords = null!;
        Maybe<string> passphrase = null;
        string encryptionKey = null!;

        var wizard = WizardBuilder.StartWith(() => new Steps.CreateWelcome.WelcomeViewModel())
            .Then(prev => new Steps.SeedWordsGeneration.SeedWordsViewModel(uiServices))
            .Then(prev => new SeedWordsConfirmationViewModel(seedWords), x => seedWords = x.Words.Value!)
            .Then(prev => new Steps.Passphrase.Create.PassphraseCreateViewModel())
            .Then(prev => new Steps.EncryptionPassword.EncryptionPasswordViewModel(), x => passphrase = x.Passphrase)
            .Then(prev => new SummaryViewModel(walletAppService,
                    walletBuilder, uiServices,
                    new WalletImportOptions(
                        seedWords,
                        passphrase,
                        encryptionKey), getNetwork)
                {
                    IsRecovery = false
                }, x => encryptionKey = x.EncryptionKey!
            )
            .Then(_ => new SuccessViewModel("Wallet created successfully", "Wallet Creation"))
            .FinishWith(_ => Unit.Default);

        return await uiServices.Dialog.ShowWizard(wizard, "Create wallet");
    }

    public async Task<Maybe<Unit>> Recover()
    {
        var seedwords = new SeedWords();
        var passphrase = "";
        var encryptionKey = "";

        var wizard = WizardBuilder
            .StartWith(() => new RecoveryWelcomeViewModel())
            .Then(prev => new RecoverySeedWordsViewModel())
            .Then(prev => new PassphraseRecoverViewModel(), prev => seedwords = prev.SeedWords)
            .Then(prev => new Steps.EncryptionPassword.EncryptionPasswordViewModel(), prev => passphrase = prev.Passphrase)
            .Then(prev => new SummaryViewModel(walletAppService, walletBuilder, uiServices, new WalletImportOptions(seedwords, passphrase, encryptionKey), getNetwork)
            {
                IsRecovery = true
            }, prev => encryptionKey = prev.EncryptionKey)
            .Then(_ => new SuccessViewModel("Wallet recovered successfully", "Wallet Recovery"))
            .FinishWith(_ => Unit.Default);

        return await uiServices.Dialog.ShowWizard(wizard, "Recover wallet");
    }
}