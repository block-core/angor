using System.Reactive.Linq;

namespace Angor.Sdk.Common;

/// <summary>
/// Extension methods for observable sequences.
/// </summary>
public static class ObservableExtensions
{
    /// <summary>
    /// Terminates the source sequence after the specified timeout by sending a virtual tick.
    /// </summary>
    /// <typeparam name="T">Type of the observable sequence.</typeparam>
    /// <param name="source">Source observable sequence.</param>
    /// <param name="timeout">TimeSpan after which the sequence completes.</param>
    /// <returns>An observable that completes either when the source completes or the timeout elapses.</returns>
    public static IObservable<T> WithDeadline<T>(this IObservable<T> source, TimeSpan timeout)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        return source.TakeUntil(Observable.Timer(timeout));
    }
}