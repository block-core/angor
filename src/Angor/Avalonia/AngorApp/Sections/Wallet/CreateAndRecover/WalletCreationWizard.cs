using System.Threading.Tasks;
using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;
using AngorApp.Sections.Wallet.CreateAndRecover.Steps.Summary;
using AngorApp.UI.Controls.Common.Success;
using ReactiveUI.Validation.Extensions;
using Zafiro.Avalonia.Dialogs.Wizards.Slim;
using Zafiro.UI.Commands;
using Zafiro.UI.Wizards.Slim.Builder;

namespace AngorApp.Sections.Wallet.CreateAndRecover;

public class WalletCreationWizard(UI.Services.UIServices uiServices, IWalletBuilder walletBuilder, IWalletAppService walletAppService, Func<BitcoinNetwork> getNetwork)
{
    public async Task<Maybe<Unit>> Start()
    {
        SeedWords seedWords = null!;
        Maybe<string> passphrase = null;
        string encryptionKey = null!;

        var wizard = WizardBuilder
            .StartWith(() => new Steps.CreateWelcome.WelcomeViewModel(), model => ReactiveCommand.Create(() => Result.Success(Unit.Default), model.IsValid).Enhance("Next"), "Create New Wallet")
            .Then(_ => new Steps.SeedWordsGeneration.SeedWordsViewModel(walletAppService, uiServices), model => ReactiveCommand.Create(() => Result.Success(model.Words.Value!).Tap(x => seedWords = x), model.IsValid).Enhance("Next"), "Seed Words")
            .Then(seedwords => new Steps.SeedWordsConfirmation.SeedWordsConfirmationViewModel(seedwords), model => ReactiveCommand.Create(() => Result.Success(Unit.Default), model.IsValid).Enhance("Next"), "Confirm Seed Words")
            .Then(_ => new Steps.Passphrase.Create.PassphraseCreateViewModel(), model => ReactiveCommand.Create(() => Result.Success(model.Passphrase).Tap(x => passphrase = x), model.IsValid()).Enhance("Next"), "Passphrase")
            .Then(_ => new Steps.EncryptionPassword.EncryptionPasswordViewModel(), model => ReactiveCommand.Create(() => Result.Success(model.EncryptionKey!).Tap(x => encryptionKey = x), model.IsValid()).Enhance("Next"), "Encryption Key")
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