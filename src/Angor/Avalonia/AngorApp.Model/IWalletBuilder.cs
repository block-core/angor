using CSharpFunctionalExtensions;

namespace AngorApp.Model;

public interface IWalletBuilder
{
    Task<Result<IWallet>> Create(SeedWords seedwords, Maybe<string> passphrase, string encryptionKey);
}