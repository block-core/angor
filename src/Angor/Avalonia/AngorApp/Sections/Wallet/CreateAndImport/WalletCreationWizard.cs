using System.Threading.Tasks;
using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;
using AngorApp.Sections.Wallet.CreateAndImport.Steps.CreateWelcome;
using AngorApp.Sections.Wallet.CreateAndImport.Steps.EncryptionPassword;
using AngorApp.Sections.Wallet.CreateAndImport.Steps.Passphrase.Create;
using AngorApp.Sections.Wallet.CreateAndImport.Steps.SeedWordsConfirmation;
using AngorApp.Sections.Wallet.CreateAndImport.Steps.SeedWordsGeneration;
using AngorApp.Sections.Wallet.CreateAndImport.Steps.Summary;
using AngorApp.UI.Controls.Common.Success;
using ReactiveUI.Validation.Extensions;
using Zafiro.Avalonia.Dialogs.Wizards.Slim;
using Zafiro.UI.Commands;
using Zafiro.UI.Wizards.Slim.Builder;

namespace AngorApp.Sections.Wallet.CreateAndImport;

public class WalletCreationWizard(UI.Services.UIServices uiServices, IWalletBuilder walletBuilder, IWalletAppService walletAppService, Func<BitcoinNetwork> getNetwork)
{
    public async Task<Maybe<Unit>> Start()
    {
        SeedWords seedWords = null!;
        Maybe<string> passphrase = null;
        string encryptionKey = null!;

        var wizard = WizardBuilder
            .StartWith(() => new WelcomeViewModel(), model => ReactiveCommand.Create(() => Result.Success(Unit.Default), model.IsValid).Enhance("Next"), "Create New Wallet")
            .Then(_ => new SeedWordsViewModel(walletAppService, uiServices), model => ReactiveCommand.Create<Result<SeedWords>>(() => Result.Success<SeedWords>(model.Words.Value!).Tap(x => seedWords = x), model.IsValid).Enhance("Next"), "Seed Words")
            .Then(seedwords => new SeedWordsConfirmationViewModel(seedwords), model => ReactiveCommand.Create(() => Result.Success(Unit.Default), model.IsValid).Enhance("Next"), "Confirm Seed Words")
            .Then(_ => new PassphraseCreateViewModel(), model => ReactiveCommand.Create<Result<string>>(() => Result.Success<string>(model.Passphrase).Tap(x => passphrase = x), model.IsValid()).Enhance("Next"), "Passphrase")
            .Then(_ => new EncryptionPasswordViewModel(), model => ReactiveCommand.Create<Result<string>>(() => Result.Success<string>(model.EncryptionKey!).Tap(x => encryptionKey = x), model.IsValid()).Enhance("Next"), "Encryption Key")
            .Then(_ => new SummaryViewModel(walletAppService,
                walletBuilder, uiServices,
                new WalletImportOptions(
                    seedWords,
                    passphrase,
                    encryptionKey), getNetwork)
            {
                IsRecovery = false
            }, model => model.CreateWallet.Enhance("Create Wallet"), "Summary")
            .Then(_ => new SuccessViewModel("Wallet created successfully"), model => ReactiveCommand.Create(() => Result.Success(Unit.Default)).Enhance("Close"), "Wallet Creation")
            .WithCompletionFinalStep();

        return await uiServices.Dialog.ShowWizard(wizard, "Create wallet").Map(_ => Unit.Default);
    }
}