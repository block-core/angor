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
        var wizard = WizardBuilder.StartWith(() => new AddressAndAmountViewModel(sourceWallet), model => ReactiveCommand.Create(() => Result.Success((model.Amount, model.Address)), model.IsValid).Enhance("Next"), "Amount and address")
            .Then(sendData => new TransactionDraftViewModel(sourceWallet.Id, walletAppService, new SendAmount("Test", sendData.Amount.Value, sendData.Address), uiServices), model => model.Confirm.Enhance("Confirm"), "Summary")
            .Then(_ => new SuccessViewModel("Transaction sent!"), _ => ReactiveCommand.Create(() => Result.Success(Unit.Default)).Enhance("Close"), "Transaction sent")
            .WithCompletionFinalStep();

        await uiServices.Dialog.ShowWizard(wizard, "Send");
    }
}