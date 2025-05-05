using Angor.Contexts.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace Angor.UI.Model;

public interface ITransactionDraft
{
    public IAmountUI TotalFee { get; set; }
    Task<Result<TxId>> Submit();
}