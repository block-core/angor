using CSharpFunctionalExtensions;

namespace Angor.Contexts.CrossCutting;

public interface IStore
{
    Task<Result> Save<T>(string key, T data);
    Task<Result<T>> Load<T>(string key);
}