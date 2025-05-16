using System.Threading.Tasks;
using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;
using AngorApp.Sections.Wallet.CreateAndRecover.Steps.RecoverySeedWords;
using AngorApp.Sections.Wallet.CreateAndRecover.Steps.RecoveryWelcome;
using AngorApp.Sections.Wallet.CreateAndRecover.Steps.Summary;
using AngorApp.UI.Controls.Common.Success;
using ReactiveUI.Validation.Extensions;
using Zafiro.Avalonia.Dialogs.Wizards.Slim;
using Zafiro.UI.Commands;
using Zafiro.UI.Wizards.Slim.Builder;

namespace AngorApp.Sections.Wallet.CreateAndRecover;

public class WalletRecoveryWizard(UI.Services.UIServices uiServices, IWalletBuilder walletBuilder, IWalletAppService walletAppService, Func<BitcoinNetwork> getNetwork)
{
    public async Task<Maybe<Unit>> Start()
    {
        SeedWords seedWords = null!;
        Maybe<string> passphrase = null;
        string encryptionKey = null!;

        var wizard = WizardBuilder
            .StartWith(() => new RecoveryWelcomeViewModel(), _ => ReactiveCommand.Create(() => Result.Success(Unit.Default)).Enhance("Next"), "Wallet Recovery")
            .Then(_ => new RecoverySeedWordsViewModel(), model => ReactiveCommand.Create(() => Result.Success(model.SeedWords).Tap(x => seedWords = x), model.IsValid).Enhance("Next"), "Seed Words")
            .Then(_ => new Steps.Passphrase.Create.PassphraseCreateViewModel(), model => ReactiveCommand.Create(() => Result.Success(model.Passphrase).Tap(x => passphrase = x), model.IsValid()).Enhance("Next"), "Passphrase")
            .Then(_ => new Steps.EncryptionPassword.EncryptionPasswordViewModel(), model => ReactiveCommand.Create(() => Result.Success(model.EncryptionKey!).Tap(x => encryptionKey = x), model.IsValid()).Enhance("Next"), "Encryption Key")
            .Then(_ => new SummaryViewModel(walletAppService,
                walletBuilder, uiServices,
                new WalletImportOptions(
                    seedWords,
                    passphrase,
                    encryptionKey), getNetwork)
            {
                IsRecovery = true
            }, model => model.CreateWallet.Enhance("Recover Wallet"), "Summary")
            .Then(_ => new SuccessViewModel("Wallet recovered successfully"), model => ReactiveCommand.Create(() => Result.Success(Unit.Default)).Enhance("Close"), "Wallet Recovery")
            .WithCompletionFinalStep();

        return await uiServices.Dialog.ShowWizard(wizard, "Recover wallet").Map(_ => Unit.Default);
    }
}