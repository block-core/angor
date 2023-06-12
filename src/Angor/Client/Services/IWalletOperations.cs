namespace Angor.Client.Services;

public interface IWalletOperations
{
    Task<(bool, string)> SendAmountToAddress(decimal sendAmount, long selectedFee, string sendToAddress);
}