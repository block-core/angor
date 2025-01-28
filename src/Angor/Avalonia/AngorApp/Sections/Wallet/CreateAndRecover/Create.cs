using System.Threading.Tasks;
using Angor.UI.Model;
using AngorApp.Sections.Wallet.CreateAndRecover.Steps.Passphrase.Create;
using AngorApp.Sections.Wallet.CreateAndRecover.Steps.SeedWordsConfirmation;
using AngorApp.Sections.Wallet.Operate;
using AngorApp.Services;
using AngorApp.UI.Controls.Common.Success;
using CSharpFunctionalExtensions;
using Zafiro.Avalonia.Controls.Wizards.Builder;
using Zafiro.Avalonia.Dialogs;
using EncryptionPasswordViewModel = AngorApp.Sections.Wallet.CreateAndRecover.Steps.EncryptionPassword.EncryptionPasswordViewModel;
using SeedWordsViewModel = AngorApp.Sections.Wallet.CreateAndRecover.Steps.SeedWordsGeneration.SeedWordsViewModel;
using SummaryAndCreationViewModel = AngorApp.Sections.Wallet.CreateAndRecover.Steps.SummaryAndCreation.SummaryAndCreationViewModel;
using WelcomeViewModel = AngorApp.Sections.Wallet.CreateAndRecover.Steps.CreateWelcome.WelcomeViewModel;

namespace AngorApp.Sections.Wallet.CreateAndRecover;

public class Create
{
    private readonly UIServices uiServices;
    private readonly IWalletBuilder walletBuilder;

    public Create(UIServices uiServices, IWalletBuilder walletBuilder)
    {
        this.uiServices = uiServices;
        this.walletBuilder = walletBuilder;
    }

    public async Task<Maybe<Result<IWallet>>> Start()
    {
        var wizardBuilder = WizardBuilder
            .StartWith(() => new WelcomeViewModel())
            .Then(prev => new SeedWordsViewModel(uiServices))
            .Then(prev => new SeedWordsConfirmationViewModel(prev.Words.Value))
            .Then(prev => new PassphraseCreateViewModel(prev.SeedWords))
            .Then(prev => new EncryptionPasswordViewModel(prev.SeedWords, prev.Passphrase!))
            .Then(prev => new SummaryAndCreationViewModel(prev.Passphrase, prev.SeedWords, prev.Password!, walletBuilder)
            {
                 IsRecovery = false,
            })
            .Then(_ => new SuccessViewModel("Wallet created successfully!", "Done"))
            .Build();

        var result = await uiServices.Dialog.Show(wizardBuilder, "Create wallet", closeable => wizardBuilder.OptionsForCloseable(closeable));
        if (result)
        {
            return Result.Success<IWallet>(new WalletDesign());
        }

        return Maybe<Result<IWallet>>.None;
    }
}