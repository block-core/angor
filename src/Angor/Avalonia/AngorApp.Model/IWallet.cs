using CSharpFunctionalExtensions;

namespace AngorApp.Model;

public interface IWallet
{
    public IEnumerable<IBroadcastedTransaction> History { get; }
    ulong? Balance { get; set; }
    public BitcoinNetwork Network { get; }
    public string ReceiveAddress { get; }
    Task<Result<IUnsignedTransaction>> CreateTransaction(ulong amount, string address, ulong feerate);
    Result IsAddressValid(string address);
}