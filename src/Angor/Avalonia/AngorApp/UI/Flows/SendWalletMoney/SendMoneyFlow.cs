using Angor.Sdk.Wallet.Application;
using AngorApp.Model.Contracts.Flows;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Dialogs.Wizards.Slim;
using Zafiro.UI.Wizards.Slim;
using Zafiro.UI.Wizards.Slim.Builder;
using AddressAndAmountViewModel = AngorApp.UI.Flows.SendWalletMoney.AddressAndAmount.AddressAndAmountViewModel;
using TransactionDraftViewModel = AngorApp.UI.Flows.SendWalletMoney.TransactionDraft.TransactionDraftViewModel;

namespace AngorApp.UI.Flows.SendWalletMoney;

public class SendMoneyFlow(IWalletAppService walletAppService, UIServices uiServices) : ISendMoneyFlow
{
    public async Task SendMoney(IWallet sourceWallet)
    {
        var wizard = WizardBuilder
            .StartWith(() => new AddressAndAmountViewModel(sourceWallet), "Amount and address").Next(model => (model.Amount, model.Address)).WhenValid<AddressAndAmountViewModel>()
            .Then(sendData => new TransactionDraftViewModel(sourceWallet.Id, walletAppService, new SendAmount("Test", sendData.Amount.Value, sendData.Address), uiServices), "Summary").NextCommand(model => model.Confirm.Enhance("Confirm"))
            .Build(StepKind.Commit);

        var result = await uiServices.Dialog.ShowWizard(wizard, "Send");

        if (result.HasValue)
        {
            await uiServices.Dialog.ShowMessage(
                "Transaction sent",
                "Transaction sent!",
                "Done",
                new Icon("fa-check"),
                DialogTone.Success);
        }
    }
}
