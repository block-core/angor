using CSharpFunctionalExtensions;

namespace Angor.Contests.CrossCutting;

public interface IStore
{
    Task<Result> Save<T>(string key, T data);
    Task<Result<T>> Load<T>(string key);
}