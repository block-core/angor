using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace Angor.UI.Model.Implementation.Wallet;

public class TransactionDraft(WalletId walletId, long amount, string address, DomainFeeRate feeRate, FeeAndSize feeAndSize, IWalletAppService walletAppService) : ITransactionDraft
{
    public WalletId WalletId { get; } = walletId;
    public long Amount { get; } = amount;
    public string Address { get; } = address;
    public IWalletAppService WalletAppService { get; } = walletAppService;
    public IAmountUI TotalFee { get; set; } = new AmountUI(feeAndSize.Fee);

    public Task<Result<TxId>> Confirm()
    {
        return WalletAppService.SendAmount(WalletId, new Amount(Amount), new Address(Address), feeRate).Map(id => id);
    }
}