using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Wallet.Infrastructure.Impl;

public class WalletStore(IStore store) : IWalletStore
{
    private const string WalletsFile = "wallets.json";

    public async Task<Result<IEnumerable<EncryptedWallet>>> GetAll() => await store.Load<List<EncryptedWallet>>(WalletsFile)
        .Map(list => list.AsEnumerable())
        .OnFailureCompensate(_ => new List<EncryptedWallet>());

    public Task<Result> SaveAll(IEnumerable<EncryptedWallet> wallets) => store.Save(WalletsFile, wallets.ToList());
}