using System.Linq;
using System.Threading.Tasks;
using Angor.Wallet.Application;
using Angor.Wallet.Domain;
using AngorApp.Sections.Browse;

namespace AngorApp.Design;

public class WalletAppServiceDesign : IWalletAppService
{
    private readonly Dictionary<WalletId, string> wallets = new();

    public async Task<Result<TxId>> SendAmount(WalletId walletId, Amount amount, Address address, DomainFeeRate feeRate)
    {
        return new TxId("1234");
    }

    public async Task<Result<IEnumerable<WalletMetadata>>> GetMetadatas()
    {
        List< WalletMetadata> metadatas = [new("<default>", WalletId.New())];
        return metadatas;
    }

    public async Task<Result<IEnumerable<(WalletId Id, string Name)>>> GetWallets()
    {
        return Result.Success(wallets.Select(x => (x.Key, x.Value)));
    }

    public async Task<Result<IEnumerable<BroadcastedTransaction>>> GetTransactions(WalletId walletId)
    {
        return Result.Failure<IEnumerable<BroadcastedTransaction>>("Not implemented yet");
    }

    public async Task<Result<Balance>> GetBalance(WalletId walletId)
    {
        return Result.Failure<Balance>("Not implemented yet");
    }

    public async Task<Result<Fee>> EstimateFee(WalletId walletId, Amount amount, Address address, DomainFeeRate feeRate)
    {
        return new Fee(12);
    }

    public async Task<Result<Address>> GetNextReceiveAddress(WalletId id)
    {
        return new Address("test address");
    }

    public string GetSeedWords()
    {
        return SampleData.Seedwords.ToString();
    }

    public async Task<Result<WalletId>> CreateWallet(string name, string seedwords, Maybe<string> passphrase, string encryptionKey, BitcoinNetwork network)
    {
        wallets.Add(WalletId.New(), name);
        return Result.Success(WalletId.New());
    }
}