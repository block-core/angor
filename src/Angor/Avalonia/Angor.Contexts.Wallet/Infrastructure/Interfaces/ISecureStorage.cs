namespace Angor.Contexts.Wallet.Infrastructure.Interfaces;

using CSharpFunctionalExtensions;

public interface ISecureStorage
{
    Result<string> Encrypt(string plainText);
    Result<string> Decrypt(string cipherText);
}
