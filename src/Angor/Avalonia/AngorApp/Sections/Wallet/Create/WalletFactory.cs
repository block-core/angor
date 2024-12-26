using System.Threading.Tasks;
using AngorApp.Common.Success;
using AngorApp.Model;
using AngorApp.Sections.Wallet.Create.Step_1;
using AngorApp.Sections.Wallet.Create.Step_2;
using AngorApp.Sections.Wallet.Create.Step_3;
using AngorApp.Sections.Wallet.Create.Step_4;
using AngorApp.Sections.Wallet.Create.Step_5;
using AngorApp.Sections.Wallet.Create.Step_6;
using AngorApp.Sections.Wallet.NoWallet;
using AngorApp.Sections.Wallet.Operate;
using AngorApp.Services;
using CSharpFunctionalExtensions;
using Zafiro.Avalonia.Controls.Wizards.Builder;
using Zafiro.Avalonia.Dialogs;
using SeedWordsViewModel = AngorApp.Sections.Wallet.Create.Step_2.SeedWordsViewModel;

namespace AngorApp.Sections.Wallet.Create;

public class WalletFactory : IWalletFactory
{
    private readonly UIServices uiServices;

    public WalletFactory(UIServices uiServices)
    {
        this.uiServices = uiServices;
    }

    public Task<Result<IWallet>> Recover()
    {
        throw new NotImplementedException();
    }

    public async Task<Maybe<Result<IWallet>>> Create()
    {
        var wizardBuilder = WizardBuilder
            .StartWith(() => new WelcomeViewModel())
            .Then(prev => new SeedWordsViewModel())
            .Then(prev => new SeedWordsConfirmationViewModel(prev.Words.Value))
            .Then(prev => new PassphraseViewModel(prev.SeedWords))
            .Then(prev => new EncryptionPasswordViewModel(prev.SeedWords, prev.Passphrase))
            .Then(prev => new SummaryAndCreationViewModel(prev.Passphrase, prev.SeedWords, prev.Password!, new WalletBuilderDesign()))
            .Then(_ => new SuccessViewModel("Wallet created successfully!"))
            .Build();

        var result = await uiServices.Dialog.Show(wizardBuilder, "Create wallet", closeable => wizardBuilder.OptionsForCloseable(closeable));
        if (result)
        {
            return Result.Success<IWallet>(new WalletDesign());
        }
        
        return Maybe<Result<IWallet>>.None;
    }
}

public class WalletBuilderDesign : IWalletBuilder
{
    public async Task<Result<IWallet>> Create(WordList seedwords, Maybe<string> passphrase, string encryptionKey)
    {
        return new WalletDesign();
    }
}