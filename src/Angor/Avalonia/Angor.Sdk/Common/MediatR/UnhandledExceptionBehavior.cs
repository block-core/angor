using System.Reflection;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Common.MediatR;

/// <summary>
/// Captures unhandled backend exceptions
/// </summary>
public class UnhandledExceptionBehavior<TRequest, TResponse>(
    ILogger<UnhandledExceptionBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception executing {RequestName}", typeof(TRequest).Name);
            return CreateFailureResult($"Angor API failed: {ex.Message}");
        }
    }

    private static TResponse CreateFailureResult(string message)
    {
        var responseType = typeof(TResponse);

        if (responseType == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(message);
        }

        if (responseType.IsGenericType &&
            responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var payloadType = responseType.GetGenericArguments()[0];
            var failureMethod = typeof(Result)
                .GetMethods(BindingFlags.Static | BindingFlags.Public)
                .First(m => m.Name == nameof(Result.Failure) && m.IsGenericMethod && m.GetParameters().Length == 1);

            var genericFailure = failureMethod.MakeGenericMethod(payloadType);
            return (TResponse)genericFailure.Invoke(null, new object[] { message })!;
        }

        throw new InvalidOperationException(
            $"Unhandled pipeline response type {responseType.FullName}. Only Result and Result<T> are supported.");
    }
}