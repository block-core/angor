using Angor.Projects.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;

namespace Angor.Projects.Tests;

public class TestingInvestorKeyProvider : IInvestorKeyProvider
{
    private readonly IDerivationOperations derivationOperations;
    private readonly string seed;
    private readonly string passphrase;

    public TestingInvestorKeyProvider(string seed, string passphrase, IDerivationOperations derivationOperations)
    {
        this.seed = seed;
        this.passphrase = passphrase;
        this.derivationOperations = derivationOperations;
    }
    
    public async Task<Result<string>> InvestorKey(Guid walletId, string founderKey)
    {
        return Result.Try(() => DeriveInvestorKey(founderKey));
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