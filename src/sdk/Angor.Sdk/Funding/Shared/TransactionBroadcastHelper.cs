using CSharpFunctionalExtensions;

namespace Angor.Sdk.Funding.Shared;

internal static class TransactionBroadcastHelper
{
    private const int MaxAttempts = 3;

    public static bool IsAlreadySubmitted(string? message) =>
        message != null &&
        (message.Contains("already in block", StringComparison.OrdinalIgnoreCase) ||
         message.Contains("already known", StringComparison.OrdinalIgnoreCase) ||
         message.Contains("txn-already-in-mempool", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Broadcasts a transaction with up to <see cref="MaxAttempts"/> retry attempts and exponential back-off.
    /// </summary>
    /// <param name="txId">Transaction identifier used in log messages and the success result.</param>
    /// <param name="broadcastAsync">
    ///     Delegate that performs the actual broadcast.
    ///     Must return <c>null</c> or an empty string on success, or an error message on failure.
    ///     Exception handling (including converting caught exceptions to error strings) is the caller's responsibility.
    /// </param>
    /// <param name="onAttemptFailed">Called after each failed attempt with (attempt, maxAttempts, errorMessage).</param>
    /// <param name="onSucceeded">Called after a successful broadcast with (attempt).</param>
    /// <param name="cancellationToken">Optional cancellation token, passed to the inter-attempt delay.</param>
    /// <returns>
    ///     <see cref="Result{T}"/> containing the transaction ID on success,
    ///     or the last error message on failure after all attempts are exhausted.
    /// </returns>
    public static async Task<Result<string>> BroadcastWithRetryAsync(
        string txId,
        Func<Task<string?>> broadcastAsync,
        Action<int, int, string> onAttemptFailed,
        Action<int> onSucceeded,
        CancellationToken cancellationToken = default)
    {
        string? lastError = null;

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var errorMessage = await broadcastAsync();

            if (string.IsNullOrEmpty(errorMessage) || IsAlreadySubmitted(errorMessage))
            {
                onSucceeded(attempt);
                return Result.Success(txId);
            }

            lastError = errorMessage;
            onAttemptFailed(attempt, MaxAttempts, errorMessage);

            if (attempt < MaxAttempts)
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), cancellationToken);
        }

        return Result.Failure<string>(lastError!);
    }
}
