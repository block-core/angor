using CSharpFunctionalExtensions;

namespace Angor.Shared.Services;

public interface ISensitiveNostrData
{
    Task<Result<string>> GetNostrPrivateKeyHex(KeyIdentifier keyIdentifier);
}