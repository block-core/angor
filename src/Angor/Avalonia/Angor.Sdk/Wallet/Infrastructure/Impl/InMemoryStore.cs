using Angor.Sdk.Common;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Wallet.Infrastructure.Impl;

public class InMemoryStore : IStore
{
    private Dictionary<string, object> dict = new();

    public Task<Result> Save<T>(string key, T data)
    {
        dict[key] = data ?? throw new ArgumentNullException(nameof(data));
        return Task.FromResult(Result.Success());
    }

    public Task<Result<T>> Load<T>(string key)
    {
        return Task.FromResult(dict.TryFind(key).ToResult("Key not found").Map(o => (T)o));
    }
}