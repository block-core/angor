using Angor.Contexts.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Wallet.Infrastructure.Interfaces;

public interface IWalletFactory
{
    Task<Result<Domain.Wallet>> CreateWallet(string name, string seedwords, Maybe<string> passphrase, Maybe<string> encryptionKey, BitcoinNetwork network);
}