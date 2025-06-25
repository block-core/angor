using System.Windows.Input;
using Angor.Contexts.Wallet.Domain;
using Humanizer;
using Zafiro.UI;

namespace AngorApp.Sections.Wallet.Operate;

public class WalletViewModelDesign : ReactiveObject, IWalletViewModel
{
    private ReactiveCommand<Unit, ResultViewModel<string>> getReceiveAddress;

    public IWallet Wallet { get; } = new WalletDesign();

    public ICommand Send { get; }

    ReactiveCommand<Unit, ResultViewModel<string>> IWalletViewModel.GetReceiveAddress => getReceiveAddress;

    public ResultViewModel<string> ReceiveAddressResult { get; }

    public IEnumerable<IdentityContainer<ITransactionViewModel>> History { get; } =
    [
        new()
        {
            Content = new TransactionViewModelDesign()
            {
                Transaction = new BroadcastedTransactionDesign()
                {
                    Balance = new AmountUI(200),
                    BlockTime = DateTimeOffset.Now,
                    Id = "12345"
                }
            },
        },
        new()
        {
            Content = new TransactionViewModelDesign()
            {
                Transaction = new BroadcastedTransactionDesign()
                {
                    Balance = new AmountUI(-400),
                    BlockTime = DateTimeOffset.Now - 1.Days(),
                    Id = "12345"
                }
            },
        },
        new()
        {
            Content = new TransactionViewModelDesign()
            {
                Transaction = new BroadcastedTransactionDesign()
                {
                    Balance = new AmountUI(0),
                    BlockTime = DateTimeOffset.Now - 2.Days(),
                    Id = "12345"
                }
            },
        },
    ];

    public string Title { get; set; } = "Testing";
}