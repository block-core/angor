using Angor.Sdk.Common;
using Angor.Shared;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Tests.TestDoubles;

public class TestingSeedwordsProvider : ISeedwordsProvider
{
    private readonly IDerivationOperations derivationOperations;
    private readonly string seed;
    private readonly string passphrase;

    public TestingSeedwordsProvider(string seed, string passphrase, IDerivationOperations derivationOperations)
    {
        this.seed = seed;
        this.passphrase = passphrase;
        this.derivationOperations = derivationOperations;
    }
    
    public async Task<Result<string>> InvestorKey(string walletId, string founderKey)
    {
        return Result.Try(() => DeriveInvestorKey(founderKey));
    }

    public async Task<Result<(string Words, Maybe<string> Passphrase)>> GetSensitiveData(string walletId)
    {
        return (seed, passphrase);
    }

    private string DeriveInvestorKey(string founderKey)
    {
        return derivationOperations.DeriveInvestorKey(new WalletWords()
        {
            Words = seed,
            Passphrase = passphrase
        }, founderKey);
    }
}