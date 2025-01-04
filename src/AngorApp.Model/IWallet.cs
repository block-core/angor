using CSharpFunctionalExtensions;

namespace AngorApp.Model;

public interface IWallet
{
    public IEnumerable<IBroadcastedTransaction> History { get; }
    decimal? Balance { get; set; }
    public BitcoinNetwork Network { get; }
    public string ReceiveAddress { get; }
    Task<Result<IUnsignedTransaction>> CreateTransaction(decimal amount, string address, decimal feerate);
    Result IsAddressValid(string address);
}