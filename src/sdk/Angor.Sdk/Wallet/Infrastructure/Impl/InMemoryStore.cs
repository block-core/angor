using Angor.Sdk.Common;
using Angor.Primitives;

namespace Angor.Sdk.Wallet.Infrastructure.Impl;

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
        if (dict.TryGetValue(key, out var value))
            return Result.Success((T)value);

        return Result.Failure<T>("Key not found");
    }
}