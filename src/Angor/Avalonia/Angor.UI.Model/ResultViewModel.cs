using CSharpFunctionalExtensions;

namespace Angor.UI.Model;

public class ResultViewModel(Result result)
{
    public bool IsFailure { get; } = result.IsFailure;

    public bool IsSuccess { get; } = result.IsSuccess;

    public string Error { get; } = result.IsFailure ? result.Error : "";
}

public class ResultViewModel<T>(Result<T> result)
{
    public T? Value { get; } = result.GetValueOrDefault();
    public bool IsFailure { get; } = result.IsFailure;

    public bool IsSuccess { get; } = result.IsSuccess;

    public string Error { get; } = result.IsFailure ? result.Error : "";
}