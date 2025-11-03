using CSharpFunctionalExtensions;

namespace AngorApp.Model.Common;

public class ResultViewModel(Result result)
{
    public bool IsFailure { get; } = result.IsFailure;

    public bool IsSuccess { get; } = result.IsSuccess;

    public string Error { get; } = result.IsFailure ? result.Error : "";
    
    public static ResultViewModel<T> From<T>(Result<T> result)
    {
        return new ResultViewModel<T>(result);
    }
}

public class ResultViewModel<T>(Result<T> result)
{
    public T? Value { get; } = result.GetValueOrDefault();
    public bool IsFailure { get; } = result.IsFailure;

    public bool IsSuccess { get; } = result.IsSuccess;

    public string Error { get; } = result.IsFailure ? result.Error : "";
}