namespace Angor.Primitives;

public readonly struct Result
{
    private readonly string? _error;

    private Result(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        _error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    public string Error => IsSuccess
        ? throw new InvalidOperationException("Cannot access Error on a successful result.")
        : _error!;

    public static Result Success() => new(true, null);
    public static Result Failure(string error) => new(false, error);

    public static Result<T> Success<T>(T value) => new(value, true, null);
    public static Result<T> Failure<T>(string error) => new(default, false, error);
}

public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly string? _error;

    public Result(T? value, bool isSuccess, string? error)
    {
        _value = value;
        IsSuccess = isSuccess;
        _error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot access Value on a failed result. Error: {_error}");

    public string Error => IsSuccess
        ? throw new InvalidOperationException("Cannot access Error on a successful result.")
        : _error!;

    public static implicit operator Result<T>(T value) => Result.Success(value);
}