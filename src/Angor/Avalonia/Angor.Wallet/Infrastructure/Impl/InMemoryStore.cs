using Angor.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Wallet.Infrastructure.Impl;

public class InMemoryStore : IStore
{
    private Dictionary<string, object> dict = new();

    public async Task<Result> Save<T>(string key, T data)
    {
        dict[key] = data ?? throw new ArgumentNullException(nameof(data));
        return Result.Success();
    }

    public async Task<Result<T>> Load<T>(string key)
    {
        return dict.TryFind(key).ToResult("Key not found").Map(o => (T)o);
    }
}