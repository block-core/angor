using System.Threading.Tasks;
using Angor.UI.Model;
using AngorApp.Sections.Wallet.CreateAndRecover.Steps.EncryptionPassword;
using AngorApp.Sections.Wallet.CreateAndRecover.Steps.Passphrase;
using AngorApp.Sections.Wallet.CreateAndRecover.Steps.Passphrase.Create;
using AngorApp.Sections.Wallet.CreateAndRecover.Steps.Passphrase.Recover;
using AngorApp.Sections.Wallet.CreateAndRecover.Steps.RecoverySeedWords;
using AngorApp.Sections.Wallet.CreateAndRecover.Steps.RecoveryWelcome;
using AngorApp.Sections.Wallet.CreateAndRecover.Steps.SummaryAndCreation;
using AngorApp.Services;
using AngorApp.UI.Controls.Common.Success;
using CSharpFunctionalExtensions;
using Zafiro.Avalonia.Controls.Wizards.Builder;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.Sections.Wallet.CreateAndRecover
{
    public class Recover
    {
        private readonly UIServices uiServices;
        private readonly IWalletBuilder walletBuilder;

        public Recover(UIServices uiServices, IWalletBuilder walletBuilder)
        {
            this.uiServices = uiServices;
            this.walletBuilder = walletBuilder;
        }

        public async Task<Maybe<Result<IWallet>>> Start()
        {
            var wizardBuilder = WizardBuilder
                .StartWith(() => new RecoveryWelcomeViewModel())
                .Then(_ => new RecoverySeedWordsViewModel())
                .Then(seedwords => new PassphraseRecoverViewModel(seedwords.SeedWords))
                .Then(passphrase => new EncryptionPasswordViewModel(passphrase.SeedWords, passphrase.Passphrase!))
                .Then(passphrase => new SummaryAndCreationViewModel(passphrase.Passphrase, passphrase.SeedWords, passphrase.Password!, walletBuilder)
                {
                    IsRecovery = true
                })
                .Then(_ => new SuccessViewModel("Wallet recovered!", "Success"))
                .Build();

            var result = await uiServices.Dialog.Show(wizardBuilder, "Recover wallet", closeable => wizardBuilder.OptionsForCloseable(closeable));
            if (result)
            {
                return Result.Success<IWallet>(new WalletDesign());
            }

            return Maybe<Result<IWallet>>.None;
        }
    }
}