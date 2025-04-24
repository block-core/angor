using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace Angor.UI.Model.Implementation.Wallet;

public class TransactionDraft(WalletId walletId, long amount, string address, DomainFeeRate feeRate, Fee fee, IWalletAppService walletAppService) : ITransactionDraft
{
    public WalletId WalletId { get; } = walletId;
    public long Amount { get; } = amount;
    public string Address { get; } = address;
    public IWalletAppService WalletAppService { get; } = walletAppService;
    public long TotalFee { get; set; } = fee.Value;

    public Task<Result<TxId>> Submit()
    {
        return WalletAppService.SendAmount(WalletId, new Amount(Amount), new Address(Address), feeRate).Map(id => id);
    }
}