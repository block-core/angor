using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Nostr.Client.Keys;
using Nostr.Client.Messages;

namespace Angor.Sdk.WalletExport.Blossom;

public sealed class BackupBlossomClient : IBackupBlossomClient
{
    private const string ContentType = "application/octet-stream";
    private const int AuthExpiryMinutes = 5;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<BackupBlossomClient> logger;

    public BackupBlossomClient(IHttpClientFactory httpClientFactory, ILogger<BackupBlossomClient> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
    }

    public async Task<Result<BlobUploadResult>> UploadAsync(
        string serverBaseUrl, byte[] blob, string expectedSha256, string nostrPrivateKeyHex,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serverBaseUrl)) return Result.Failure<BlobUploadResult>("Empty server URL.");
        if (blob is null || blob.Length == 0) return Result.Failure<BlobUploadResult>("Empty blob.");
        if (string.IsNullOrWhiteSpace(expectedSha256)) return Result.Failure<BlobUploadResult>("Missing expected hash.");

        var baseUrl = serverBaseUrl.TrimEnd('/');
        var uploadUrl = $"{baseUrl}/upload";

        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromMinutes(2);

            using var content = new ByteArrayContent(blob);
            content.Headers.ContentType = new MediaTypeHeaderValue(ContentType);

            using var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl) { Content = content };
            request.Headers.Add("Authorization", BuildAuthorizationHeader(nostrPrivateKeyHex, expectedSha256));

            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var reason = response.Headers.Contains("X-Reason")
                    ? response.Headers.GetValues("X-Reason").FirstOrDefault()
                    : body;
                logger.LogWarning("Blossom backup upload to {Server} failed {Status}: {Reason}",
                    baseUrl, response.StatusCode, reason);
                return Result.Failure<BlobUploadResult>($"Upload failed ({(int)response.StatusCode}): {reason}");
            }

            var descriptor = JsonSerializer.Deserialize<BlobDescriptor>(body, JsonOptions);
            if (descriptor?.Url is null || descriptor.Sha256 is null)
                return Result.Failure<BlobUploadResult>("Server returned an invalid descriptor.");

            if (!string.Equals(descriptor.Sha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
                return Result.Failure<BlobUploadResult>(
                    $"Server returned hash {descriptor.Sha256} but expected {expectedSha256}.");

            return Result.Success(new BlobUploadResult(baseUrl, descriptor.Url, descriptor.Sha256.ToLowerInvariant(), descriptor.Size));
        }
        catch (TaskCanceledException)
        {
            return Result.Failure<BlobUploadResult>("Upload was cancelled.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Network error uploading backup blob to {Server}", baseUrl);
            return Result.Failure<BlobUploadResult>($"Network error: {ex.Message}");
        }
    }

    public async Task<Result<bool>> ExistsAsync(string serverBaseUrl, string sha256, CancellationToken cancellationToken = default)
    {
        var baseUrl = serverBaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/{sha256}";

        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await client.SendAsync(request, cancellationToken);

            return Result.Success(response.IsSuccessStatusCode);
        }
        catch (TaskCanceledException)
        {
            return Result.Failure<bool>("Probe was cancelled.");
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<bool>($"Network error: {ex.Message}");
        }
    }

    public async Task<Result<byte[]>> DownloadAsync(string serverBaseUrl, string sha256, CancellationToken cancellationToken = default)
    {
        var baseUrl = serverBaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/{sha256}";

        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromMinutes(1);

            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Result.Failure<byte[]>($"Download failed ({(int)response.StatusCode}).");

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var actualHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!string.Equals(actualHash, sha256, StringComparison.OrdinalIgnoreCase))
                return Result.Failure<byte[]>($"Hash mismatch: server returned {actualHash} for {sha256}.");

            return Result.Success(bytes);
        }
        catch (TaskCanceledException)
        {
            return Result.Failure<byte[]>("Download was cancelled.");
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<byte[]>($"Network error: {ex.Message}");
        }
    }

    /// <summary>
    /// BUD-02 auth event: kind 24242 signed by the passphrase-derived nsec.
    /// Tagged with <c>t=upload</c>, <c>x=&lt;sha256&gt;</c>, and a short expiration.
    /// </summary>
    private static string BuildAuthorizationHeader(string nostrPrivateKeyHex, string sha256)
    {
        var key = NostrPrivateKey.FromHex(nostrPrivateKeyHex);
        var expiration = DateTimeOffset.UtcNow.AddMinutes(AuthExpiryMinutes).ToUnixTimeSeconds().ToString();

        var authEvent = new NostrEvent
        {
            Kind = (NostrKind)24242,
            CreatedAt = DateTime.UtcNow,
            Content = "Angor backup upload",
            Tags = new NostrEventTags(
                new NostrEventTag("t", "upload"),
                new NostrEventTag("x", sha256.ToLowerInvariant()),
                new NostrEventTag("expiration", expiration))
        }.Sign(key);

        var eventJson = JsonSerializer.Serialize(new
        {
            id = authEvent.Id,
            pubkey = authEvent.Pubkey,
            created_at = new DateTimeOffset(authEvent.CreatedAt ?? DateTime.UtcNow).ToUnixTimeSeconds(),
            kind = (int)authEvent.Kind,
            tags = authEvent.Tags?.Select(t =>
            {
                var values = new List<string> { t.TagIdentifier };
                if (t.AdditionalData != null) values.AddRange(t.AdditionalData);
                return values.ToArray();
            }).ToArray() ?? Array.Empty<string[]>(),
            content = authEvent.Content ?? string.Empty,
            sig = authEvent.Sig
        }, JsonOptions);

        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(eventJson));
        return $"Nostr {base64}";
    }

    private sealed class BlobDescriptor
    {
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("sha256")] public string? Sha256 { get; set; }
        [JsonPropertyName("size")] public long Size { get; set; }
        [JsonPropertyName("type")] public string? Type { get; set; }
    }
}
