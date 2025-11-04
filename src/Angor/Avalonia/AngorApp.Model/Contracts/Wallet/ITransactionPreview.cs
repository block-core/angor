using Angor.Contexts.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace AngorApp.Model.Contracts.Wallet;

public interface ITransactionDraft
{
    public IAmountUI TotalFee { get; set; }
    Task<Result<TxId>> Confirm();
}