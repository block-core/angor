namespace Angor.Shared.Models;

public class OperationResult
{
    public string? Message { get; set; }
    public bool Success { get; set; }
}

public class SuccessOperationResult : OperationResult
{
    public SuccessOperationResult()
    {
        Success = true;
    }
}

public class OperationResult<T> : OperationResult 
{
    public T? Data { get; set; }
}