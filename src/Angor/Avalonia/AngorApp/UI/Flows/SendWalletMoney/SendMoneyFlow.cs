using Angor.Contexts.Wallet.Application;
using AngorApp.Model.Contracts.Flows;
using AngorApp.UI.Shared.Controls.Common.Success;
using Branta.V2.Classes;
using Zafiro.Avalonia.Dialogs.Wizards.Slim;
using Zafiro.UI.Wizards.Slim.Builder;
using AddressAndAmountViewModel = AngorApp.UI.Flows.SendWalletMoney.AddressAndAmount.AddressAndAmountViewModel;
using TransactionDraftViewModel = AngorApp.UI.Flows.SendWalletMoney.TransactionDraft.TransactionDraftViewModel;

namespace AngorApp.UI.Flows.SendWalletMoney;

public class SendMoneyFlow(IWalletAppService walletAppService, UIServices uiServices, BrantaClient brantaClient) : ISendMoneyFlow
{
    public async Task SendMoney(IWallet sourceWallet)
    {
        var wizard = WizardBuilder
            .StartWith(() => new AddressAndAmountViewModel(sourceWallet, brantaClient), "Amount and address").Next(model => (model.Amount, model.Address)).WhenValid<AddressAndAmountViewModel>()
            .Then(sendData => new TransactionDraftViewModel(sourceWallet.Id, walletAppService, new SendAmount("Test", sendData.Amount.Value, sendData.Address), uiServices), "Summary").NextCommand(model => model.Confirm.Enhance("Confirm"))
            .Then(_ => new SuccessViewModel("Transaction sent!"), "Transaction sent").NextUnit("Close").Always()
            .WithCompletionFinalStep();

        await uiServices.Dialog.ShowWizard(wizard, "Send");
    }
}