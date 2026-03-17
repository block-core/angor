using Angor.Sdk.Wallet.Application;
using Angor.Sdk.Wallet.Domain;
using AngorApp.Model.Contracts.Flows;
using System.Reactive.Threading.Tasks;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Wizards.Graph.Core;
using AddressAndAmountViewModel = AngorApp.UI.Flows.SendWalletMoney.AddressAndAmount.AddressAndAmountViewModel;
using TransactionDraftViewModel = AngorApp.UI.Flows.SendWalletMoney.TransactionDraft.TransactionDraftViewModel;

namespace AngorApp.UI.Flows.SendWalletMoney;

public class SendMoneyFlow(IWalletAppService walletAppService, UIServices uiServices) : ISendMoneyFlow
{
    public async Task SendMoney(IWallet sourceWallet)
    {
        var wizard = CreateWizard(sourceWallet);

        var result = await wizard.ShowInDialog(uiServices.Dialog, "Send");

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

    private GraphWizard<TxId> CreateWizard(IWallet sourceWallet)
    {
        var flow = GraphWizard.For<TxId>();
        var addressAndAmount = new AddressAndAmountViewModel(sourceWallet);

        var addressNode = flow
            .Step(addressAndAmount, "Amount and address")
            .Next(
                model =>
                {
                    var draft = new TransactionDraftViewModel(
                        sourceWallet.Id,
                        walletAppService,
                        new SendAmount("Test", model.Amount!.Value, model.Address!),
                        uiServices);

                    return flow
                        .Step(draft, "Summary")
                        .Finish(vm => vm.Confirm.Execute().ToTask(), draft.Confirm.CanExecute, "Confirm")
                        .Build();
                },
                canExecute: addressAndAmount.IsValid)
            .Build();

        return new GraphWizard<TxId>(addressNode);
    }
}
