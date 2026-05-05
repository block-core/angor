using Angor.Primitives;

namespace Angor.Sdk.Common;

public interface ISeedwordsProvider
{
    Task<Result<(string Words, string? Passphrase)>> GetSensitiveData(string walletId);
}