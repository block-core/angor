using Angor.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace Angor.UI.Model;

public interface IUnsignedTransaction
{
    public long TotalFee { get; set; }
    Task<Result<TxId>> Accept();
}