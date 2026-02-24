using Angor.Sdk.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Wallet.Infrastructure.Interfaces;

public interface IWalletFactory
{
    Task<Result<Domain.Wallet>> CreateWallet(string name, string seedwords, Maybe<string> passphrase, BitcoinNetwork network);
}