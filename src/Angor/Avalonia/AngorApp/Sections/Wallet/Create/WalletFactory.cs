using System.Threading.Tasks;
using AngorApp.Common.Success;
using AngorApp.Model;
using AngorApp.Sections.Wallet.Create.Step1;
using AngorApp.Sections.Wallet.Create.Step2;
using AngorApp.Sections.Wallet.Create.Step3;
using AngorApp.Sections.Wallet.Create.Step4;
using AngorApp.Sections.Wallet.Create.Step5;
using AngorApp.Sections.Wallet.Create.Step6;
using AngorApp.Sections.Wallet.Create.Step3;
using AngorApp.Sections.Wallet.NoWallet;
using AngorApp.Sections.Wallet.Operate;
using AngorApp.Services;
using CSharpFunctionalExtensions;
using Zafiro.Avalonia.Controls.Wizards.Builder;
using Zafiro.Avalonia.Dialogs;
using EncryptionPasswordViewModel = AngorApp.Sections.Wallet.Create.Step5.EncryptionPasswordViewModel;
using PassphraseViewModel = AngorApp.Sections.Wallet.Create.Step4.PassphraseViewModel;
using SeedWordsViewModel = AngorApp.Sections.Wallet.Create.Step2.SeedWordsViewModel;
using SummaryAndCreationViewModel = AngorApp.Sections.Wallet.Create.Step6.SummaryAndCreationViewModel;
using WelcomeViewModel = AngorApp.Sections.Wallet.Create.Step1.WelcomeViewModel;

namespace AngorApp.Sections.Wallet.Create;

public class WalletFactory : IWalletFactory
{
    private readonly UIServices uiServices;
    private IWalletBuilder walletBuilder;

    public WalletFactory(IWalletBuilder walletBuilder, UIServices uiServices)
    {
        this.walletBuilder = walletBuilder;
        this.uiServices = uiServices;
    }

    public async Task<Maybe<Result<IWallet>>> Recover()
    {
        await uiServices.NotificationService.Show("Not implemented yet", "Not implemented yet");
        return Maybe<Result<IWallet>>.None;
    }

    public async Task<Maybe<Result<IWallet>>> Create()
    {
        var wizardBuilder = WizardBuilder
            .StartWith(() => new WelcomeViewModel())
            .Then(prev => new SeedWordsViewModel())
            .Then(prev => new SeedWordsConfirmationViewModel(prev.Words.Value))
            .Then(prev => new PassphraseViewModel(prev.SeedWords))
            .Then(prev => new EncryptionPasswordViewModel(prev.SeedWords, prev.Passphrase!))
            .Then(prev => new SummaryAndCreationViewModel(prev.Passphrase, prev.SeedWords, prev.Password!, walletBuilder))
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