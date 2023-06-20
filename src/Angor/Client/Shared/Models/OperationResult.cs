namespace Angor.Client.Shared.Models;

public class OperationResult
{
    public string? Message { get; set; }
    public bool Success { get; set; }
}

public class OperationResult<T> : OperationResult 
{
    public T? Data { get; set; }
}