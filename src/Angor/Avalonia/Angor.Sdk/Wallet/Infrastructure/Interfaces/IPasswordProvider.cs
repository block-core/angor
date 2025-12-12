using Angor.Sdk.Common;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Wallet.Infrastructure.Interfaces;

public interface IPasswordProvider
{
    Task<Maybe<string>> Get(WalletId walletId);
}