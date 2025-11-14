using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Angor.Contests.CrossCutting;
using Angor.Shared;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Wallet.Infrastructure.Impl;

public class WalletStore : IWalletStore
{
    private const string WalletsFile = "wallets.json";

    private readonly IStore store;
    private readonly INetworkStorage networkStorage;

    public WalletStore(IStore store, INetworkStorage networkStorage)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.networkStorage = networkStorage ?? throw new ArgumentNullException(nameof(networkStorage));
    }

    public async Task<Result<IEnumerable<EncryptedWallet>>> GetAll()
    {
        var key = GetScopedWalletsKey();
        var result = await store.Load<List<EncryptedWallet>>(key);
        return result.Map(list => list.AsEnumerable())
            .OnFailureCompensate(_ => new List<EncryptedWallet>());
    }

    public async Task<Result> SaveAll(IEnumerable<EncryptedWallet> wallets)
    {
        var key = GetScopedWalletsKey();
        return await store.Save(key, wallets.ToList());
    }

    private string GetScopedWalletsKey()
    {
        var network = networkStorage.GetNetwork();
        return Path.Combine(network, WalletsFile);
    }
}
