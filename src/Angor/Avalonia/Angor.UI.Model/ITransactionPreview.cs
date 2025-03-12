using Angor.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace Angor.UI.Model;

public interface ITransactionPreview
{
    public long TotalFee { get; set; }
    Task<Result<TxId>> Accept();
}