using Angor.Wallet.Application;
using Angor.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace Angor.UI.Model.Implementation.Wallet;

public class TransactionDraft(WalletId walletId, long amount, string address, long feeRate, Fee fee, IWalletAppService walletAppService) : ITransactionDraft
{
    public WalletId WalletId { get; } = walletId;
    public long Amount { get; } = amount;
    public string Address { get; } = address;
    public IWalletAppService WalletAppService { get; } = walletAppService;
    public long TotalFee { get; set; } = fee.Value;

    public Task<Result<TxId>> Submit()
    {
        return WalletAppService.SendAmount(WalletId, new Amount(Amount), new Address(Address), new DomainFeeRate(feeRate)).Map(id => id);
    }
}