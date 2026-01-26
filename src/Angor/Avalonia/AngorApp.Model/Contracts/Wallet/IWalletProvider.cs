using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace AngorApp.Model.Contracts.Wallet;

public interface IWalletProvider
{
    Task<Result<IWallet>> Get(WalletId walletId);
}