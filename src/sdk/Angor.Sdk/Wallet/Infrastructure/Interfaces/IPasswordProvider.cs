using Angor.Sdk.Common;
using Angor.Primitives;

namespace Angor.Sdk.Wallet.Infrastructure.Interfaces;

public interface IPasswordProvider
{
    Task<string?> Get(WalletId walletId);
}