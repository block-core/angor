using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using Angor.Primitives;

namespace Angor.Sdk.Wallet.Infrastructure.Impl;

public class WalletStore(IStore store) : IWalletStore
{
    private const string WalletsFile = "wallets.json";

    public async Task<Result<IEnumerable<EncryptedWallet>>> GetAll()
    {
        var result = await store.Load<List<EncryptedWallet>>(WalletsFile);
        if (result.IsFailure)
            return Result.Success<IEnumerable<EncryptedWallet>>(new List<EncryptedWallet>());

        return Result.Success(result.Value.AsEnumerable());
    }

    public Task<Result> SaveAll(IEnumerable<EncryptedWallet> wallets) => store.Save(WalletsFile, wallets.ToList());
}