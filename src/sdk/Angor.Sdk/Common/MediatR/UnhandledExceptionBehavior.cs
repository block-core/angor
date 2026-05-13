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
    private static readonly Func<string, TResponse>? FailureFactory = BuildFailureFactory();

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

            if (FailureFactory == null)
            {
                throw new InvalidOperationException(
                    $"Unhandled pipeline response type {typeof(TResponse).FullName}. Only Result and Result<T> are supported.");
            }

            return FailureFactory($"Angor API failed: {ex.Message}");
        }
    }

    private static Func<string, TResponse>? BuildFailureFactory()
    {
        if (typeof(TResponse) == typeof(Result))
        {
            return message => (TResponse)(object)Result.Failure(message);
        }

        if (typeof(TResponse).IsGenericType &&
            typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
        {
            // MakeGenericMethod is used once during static initialization (not per-call),
            // and the result is cached as a typed delegate. The AOT compiler can resolve
            // this because all closed generic instantiations are known from DI registration.
            var helperMethod = typeof(UnhandledExceptionBehavior<TRequest, TResponse>)
                .GetMethod(nameof(CreateTypedFailure), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            if (helperMethod != null)
            {
                var payloadType = typeof(TResponse).GetGenericArguments()[0];
                var closedMethod = helperMethod.MakeGenericMethod(payloadType);
                return (Func<string, TResponse>)Delegate.CreateDelegate(typeof(Func<string, TResponse>), closedMethod);
            }
        }

        return null;
    }

    private static TResponse CreateTypedFailure<TPayload>(string message)
    {
        return (TResponse)(object)Result.Failure<TPayload>(message);
    }
}