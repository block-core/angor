using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace App.UI.Shared.Helpers;

/// <summary>
/// Singleton image cache that downloads remote images once, persists them to disk,
/// and returns shared <see cref="Bitmap"/> instances keyed by URL.
/// This eliminates the duplicate-download and reload-on-attach problems caused by
/// individual AdvancedImage controls each managing their own download lifecycle.
/// </summary>
public static class ImageCacheService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private static ILogger? _logger;
    private static ILogger Logger => _logger ??= App.Services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(ImageCacheService));

    /// <summary>
    /// In-memory cache: URL → Bitmap. Once a bitmap is loaded it stays in RAM
    /// for the lifetime of the app (same as the old RamCachedWebImageLoader).
    /// </summary>
    private static readonly ConcurrentDictionary<string, Bitmap> MemoryCache = new();

    /// <summary>
    /// Tracks in-flight downloads so the same URL is never downloaded twice concurrently.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Task<Bitmap?>> InFlight = new();

    /// <summary>
    /// Disk cache directory — sits alongside the app's local data.
    /// </summary>
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "App", "ImageCache");

    /// <summary>
    /// Get a bitmap for the given URL. Returns the cached instance if available,
    /// otherwise downloads (with disk caching) and decodes it.
    /// Thread-safe — multiple concurrent requests for the same URL will share a single download.
    /// </summary>
    public static async Task<Bitmap?> GetBitmapAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        // 1. Check memory cache
        if (MemoryCache.TryGetValue(url, out var cached))
            return cached;

        // 2. Download (or join existing in-flight request)
        var bitmap = await InFlight.GetOrAdd(url, DownloadAndCacheAsync);

        // Clean up the in-flight tracker
        InFlight.TryRemove(url, out _);

        return bitmap;
    }

    /// <summary>
    /// Fire-and-forget helper: loads the bitmap and invokes a callback on the UI thread.
    /// Ideal for calling from property setters / constructors in ViewModels.
    /// </summary>
    public static void LoadBitmapAsync(string? url, Action<Bitmap?> onLoaded)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            onLoaded(null);
            return;
        }

        // Fast path: already in memory
        if (MemoryCache.TryGetValue(url, out var cached))
        {
            onLoaded(cached);
            return;
        }

        // Slow path: download in background, dispatch result to UI thread
        Task.Run(async () =>
        {
            try
            {
                var bitmap = await GetBitmapAsync(url);
                Dispatcher.UIThread.Post(() => onLoaded(bitmap));
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "LoadBitmapAsync failed for '{Url}'", url);
                Dispatcher.UIThread.Post(() => onLoaded(null));
            }
        });
    }

    private static async Task<Bitmap?> DownloadAndCacheAsync(string url)
    {
        try
        {
            // Ensure cache directory exists
            Directory.CreateDirectory(CacheDir);

            var cacheFile = GetCacheFilePath(url);

            // 1. Try loading from disk cache
            if (File.Exists(cacheFile))
            {
                try
                {
                    await using var fs = File.OpenRead(cacheFile);
                    var bitmap = new Bitmap(fs);
                    MemoryCache.TryAdd(url, bitmap);
                    return bitmap;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Corrupted cache file for '{Url}'", url);
                    try { File.Delete(cacheFile); }
                    catch (Exception deleteEx)
                    {
                        Logger.LogWarning(deleteEx, "Failed to delete corrupted cache file for '{Url}'", url);
                    }
                }
            }

            // 2. Download from network
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
                return null;

            var bytes = await response.Content.ReadAsByteArrayAsync();
            if (bytes.Length == 0)
                return null;

            // 3. Persist to disk (fire-and-forget, don't block bitmap creation)
            _ = Task.Run(() =>
            {
                try
                {
                    File.WriteAllBytes(cacheFile, bytes);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Disk write failure for cache file");
                }
            });

            // 4. Decode bitmap
            using var ms = new MemoryStream(bytes);
            var bmp = new Bitmap(ms);
            MemoryCache.TryAdd(url, bmp);
            return bmp;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "DownloadAndCacheAsync failed for '{Url}'", url);
            return null;
        }
    }

    /// <summary>
    /// Generates a deterministic file path for a URL using SHA256 hash.
    /// Preserves the original file extension for debugging convenience.
    /// </summary>
    private static string GetCacheFilePath(string url)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant();
        var ext = Path.GetExtension(new Uri(url).AbsolutePath);
        if (string.IsNullOrEmpty(ext)) ext = ".bin";
        return Path.Combine(CacheDir, hash + ext);
    }
}
