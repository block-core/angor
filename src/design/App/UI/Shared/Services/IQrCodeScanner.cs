using System.Threading;

namespace App.UI.Shared.Services;

/// <summary>Platform camera QR scanner. Decoding is local; scanned data never leaves the device.</summary>
public interface IQrCodeScanner
{
    bool IsAvailable { get; }
    Task<string?> ScanAsync(CancellationToken cancellationToken = default);
}

public sealed class UnavailableQrCodeScanner : IQrCodeScanner
{
    public bool IsAvailable => false;

    public Task<string?> ScanAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }
}
