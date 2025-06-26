using Angor.Contexts.Funding.Shared;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Tests.TestDoubles;

public class TestingSeedwordsProvider : ISeedwordsProvider
{
    private readonly string seed;
    private readonly string passphrase;

    public TestingSeedwordsProvider(string seed, string passphrase)
    {
        this.seed = seed;
        this.passphrase = passphrase;
    }

    public async Task<Result<(string Words, Maybe<string> Passphrase)>> GetSensitiveData(Guid walletId)
    {
        return (seed, passphrase);
    }
}