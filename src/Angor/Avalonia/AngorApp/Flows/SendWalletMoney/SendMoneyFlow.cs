using Angor.Contexts.Wallet.Application;
using Angor.UI.Model.Flows;
using AngorApp.UI.Controls.Common.Success;
using Zafiro.Avalonia.Dialogs.Wizards.Slim;
using Zafiro.UI.Wizards.Slim.Builder;
using AddressAndAmountViewModel = AngorApp.Flows.SendWalletMoney.AddressAndAmount.AddressAndAmountViewModel;
using TransactionDraftViewModel = AngorApp.Flows.SendWalletMoney.TransactionDraft.TransactionDraftViewModel;

namespace AngorApp.Flows.SendWalletMoney;

public class SendMoneyFlow(IWalletAppService walletAppService, UIServices uiServices) : ISendMoneyFlow
{
    public async Task SendMoney(IWallet sourceWallet)
    {
        var wizard = WizardBuilder
            .StartWith(() => new AddressAndAmountViewModel(sourceWallet), "Amount and address").Next(model => (model.Amount, model.Address)).WhenValid<AddressAndAmountViewModel>()
            .Then(sendData => new TransactionDraftViewModel(sourceWallet.Id, walletAppService, new SendAmount("Test", sendData.Amount.Value, sendData.Address), uiServices), "Summary").NextCommand(model => model.Confirm.Enhance("Confirm"))
            .Then(_ => new SuccessViewModel("Transaction sent!"), "Transaction sent").NextUnit("Close").Always()
            .WithCompletionFinalStep();

        await uiServices.Dialog.ShowWizard(wizard, "Send");
    }
}