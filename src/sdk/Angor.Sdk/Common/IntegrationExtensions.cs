using Angor.Shared.Models;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Common;

public static class IntegrationExtensions
{
    public static WalletWords ToWalletWords(this (string Words, Maybe<string> Passphrase) sensitiveData)
    {
        return new WalletWords
        {
            Words = sensitiveData.Words,
            Passphrase = sensitiveData.Passphrase.GetValueOrDefault(""),
        };
    }
}