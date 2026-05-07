using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Nostr.Client.Keys;
using Nostr.Client.Messages;

namespace App.UI.Shared.Services;

/// <summary>
/// Uploads blobs to Blossom-compatible servers per BUD-02 (PUT /upload with raw binary body).
/// When a Nostr private key is provided, a kind 24242 authorization event is created
/// and sent as an <c>Authorization: Nostr &lt;base64&gt;</c> header.
/// The server returns a JSON descriptor containing the download URL.
/// </summary>
public class BlossomUploadService
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<BlossomUploadService> logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public BlossomUploadService(IHttpClientFactory httpClientFactory, ILogger<BlossomUploadService> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
    }

    /// <summary>
    /// Uploads file bytes to the specified Blossom server.
    /// </summary>
    /// <param name="serverBaseUrl">Base URL of the Blossom server, e.g. https://nostr.build</param>
    /// <param name="fileBytes">Raw bytes of the file to upload</param>
    /// <param name="contentType">MIME type, e.g. image/png</param>
    /// <param name="nostrPrivateKeyHex">Optional Nostr private key (hex) for BUD-02 auth</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The public download URL of the uploaded blob on success.</returns>
    public async Task<Result<string>> UploadAsync(
        string serverBaseUrl,
        byte[] fileBytes,
        string contentType,
        string? nostrPrivateKeyHex = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var baseUrl = serverBaseUrl.TrimEnd('/');
            var uploadUrl = $"{baseUrl}/upload";

            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromMinutes(5);

            using var content = new ByteArrayContent(fileBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            logger.LogInformation("Uploading {Size} bytes to Blossom server {Url}", fileBytes.Length, uploadUrl);

            using var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl) { Content = content };

            // Add BUD-02 authorization header if a Nostr key is provided
            if (!string.IsNullOrEmpty(nostrPrivateKeyHex))
            {
                var authHeader = CreateAuthorizationHeader(nostrPrivateKeyHex, fileBytes, uploadUrl);
                request.Headers.Add("Authorization", authHeader);
                logger.LogInformation("Added Nostr authorization header for Blossom upload");
            }

            using var response = await client.SendAsync(request, cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var reason = response.Headers.Contains("X-Reason")
                    ? response.Headers.GetValues("X-Reason").FirstOrDefault()
                    : responseBody;
                logger.LogWarning("Blossom upload failed {StatusCode}: {Reason}", response.StatusCode, reason);
                return Result.Failure<string>($"Upload failed ({(int)response.StatusCode}): {reason}");
            }

            var descriptor = JsonSerializer.Deserialize<BlobDescriptorResponse>(responseBody, JsonOptions);
            if (descriptor?.Url is null)
            {
                logger.LogWarning("Blossom upload returned invalid response: {Response}", responseBody);
                return Result.Failure<string>("Server returned an invalid response");
            }

            logger.LogInformation("Blossom upload successful: {Url}", descriptor.Url);
            return Result.Success(descriptor.Url);
        }
        catch (TaskCanceledException)
        {
            return Result.Failure<string>("Upload was cancelled");
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Network error during Blossom upload");
            return Result.Failure<string>($"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during Blossom upload");
            return Result.Failure<string>($"Upload error: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a BUD-02 authorization header: signs a kind 24242 Nostr event and
    /// returns <c>Nostr &lt;base64-encoded-signed-event&gt;</c>.
    /// </summary>
    private string CreateAuthorizationHeader(string nostrPrivateKeyHex, byte[] fileBytes, string uploadUrl)
    {
        var key = NostrPrivateKey.FromHex(nostrPrivateKeyHex);
        var fileSha256 = Convert.ToHexString(SHA256.HashData(fileBytes)).ToLowerInvariant();
        var expiration = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds().ToString();

        var authEvent = new NostrEvent
        {
            Kind = (NostrKind)24242,
            CreatedAt = DateTime.UtcNow,
            Content = "Upload file",
            Tags = new NostrEventTags(
                new NostrEventTag("t", "upload"),
                new NostrEventTag("x", fileSha256),
                new NostrEventTag("expiration", expiration))
        }.Sign(key);

        // Serialize the signed event to JSON and base64-encode it
        var eventJson = JsonSerializer.Serialize(new
        {
            id = authEvent.Id,
            pubkey = authEvent.Pubkey,
            created_at = new DateTimeOffset(authEvent.CreatedAt ?? DateTime.UtcNow).ToUnixTimeSeconds(),
            kind = (int)authEvent.Kind,
            tags = authEvent.Tags?.Select(t =>
            {
                var values = new List<string> { t.TagIdentifier };
                if (t.AdditionalData != null)
                    values.AddRange(t.AdditionalData);
                return values.ToArray();
            }).ToArray() ?? Array.Empty<string[]>(),
            content = authEvent.Content ?? "",
            sig = authEvent.Sig
        }, JsonOptions);

        var base64Event = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(eventJson));
        return $"Nostr {base64Event}";
    }

    private sealed class BlobDescriptorResponse
    {
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("sha256")] public string? Sha256 { get; set; }
        [JsonPropertyName("size")] public long Size { get; set; }
        [JsonPropertyName("type")] public string? Type { get; set; }
    }
}
