using Angor.Shared.Models;
using Angor.Primitives;

namespace Angor.Sdk.Common;

public static class IntegrationExtensions
{
    public static WalletWords ToWalletWords(this (string Words, string? Passphrase) sensitiveData)
    {
        return new WalletWords
        {
            Words = sensitiveData.Words,
            Passphrase = sensitiveData.Passphrase ?? "",
        };
    }
}