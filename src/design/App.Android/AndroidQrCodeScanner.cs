using Android.App;
using Android.Content;
using App.UI.Shared.Services;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace App.Android;

public sealed class AndroidQrCodeScanner : IQrCodeScanner
{
    private static readonly ConcurrentDictionary<string, TaskCompletionSource<string?>> Pending = new();

    public bool IsAvailable => MainActivity.Current != null;

    public Task<string?> ScanAsync(CancellationToken cancellationToken = default)
    {
        MainActivity? activity = MainActivity.Current;
        if (activity == null)
            return Task.FromResult<string?>(null);

        string requestId = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Pending[requestId] = completion;

        CancellationTokenRegistration registration = cancellationToken.Register(() => Complete(requestId, null));
        completion.Task.ContinueWith(_ => registration.Dispose(), TaskScheduler.Default);

        var intent = new Intent(activity, typeof(QrScannerActivity));
        intent.PutExtra(QrScannerActivity.RequestIdExtra, requestId);
        activity.StartActivity(intent);
        return completion.Task;
    }

    internal static void Complete(string? requestId, string? value)
    {
        if (requestId != null && Pending.TryRemove(requestId, out var completion))
            completion.TrySetResult(value);
    }
}
