using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Wallet.Infrastructure.Impl;

public class NotSupportedSecureStorage : ISecureStorage
{
    public Result<string> Encrypt(string plainText) => Result.Failure<string>("Secure storage is not supported on this platform.");
    public Result<string> Decrypt(string cipherText) => Result.Failure<string>("Secure storage is not supported on this platform.");
}
