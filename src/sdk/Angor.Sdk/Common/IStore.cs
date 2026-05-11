using Angor.Primitives;

namespace Angor.Sdk.Common;

public interface IStore
{
    Task<Result> Save<T>(string key, T data);
    Task<Result<T>> Load<T>(string key);
}