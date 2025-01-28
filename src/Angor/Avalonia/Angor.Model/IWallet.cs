using CSharpFunctionalExtensions;

namespace AngorApp.Model;

public interface IWallet
{
    public IEnumerable<IBroadcastedTransaction> History { get; }
    long? Balance { get; set; }
    public BitcoinNetwork Network { get; }
    public string ReceiveAddress { get; }
    Task<Result<IUnsignedTransaction>> CreateTransaction(long amount, string address, long feerate);
    Result IsAddressValid(string address);
}