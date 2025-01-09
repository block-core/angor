using CSharpFunctionalExtensions;

namespace AngorApp.Model;

public interface IWalletBuilder
{
    Task<Result<IWallet>> Create(WordList seedwords, Maybe<string> passphrase, string encryptionKey);
}