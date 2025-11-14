using Angor.Shared.Models;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.CrossCutting;

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