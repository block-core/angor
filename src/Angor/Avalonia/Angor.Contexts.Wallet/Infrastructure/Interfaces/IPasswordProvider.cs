using Angor.Contexts.CrossCutting;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Wallet.Infrastructure.Interfaces;

public interface IPasswordProvider
{
    Task<Maybe<string>> Get(WalletId walletId);
}