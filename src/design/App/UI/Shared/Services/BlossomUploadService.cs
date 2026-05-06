using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace App.UI.Shared.Services;

/// <summary>
/// Uploads blobs to Blossom-compatible servers per BUD-02 (PUT /upload with raw binary body).
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
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The public download URL of the uploaded blob on success.</returns>
    public async Task<Result<string>> UploadAsync(
        string serverBaseUrl,
        byte[] fileBytes,
        string contentType,
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

    private sealed class BlobDescriptorResponse
    {
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("sha256")] public string? Sha256 { get; set; }
        [JsonPropertyName("size")] public long Size { get; set; }
        [JsonPropertyName("type")] public string? Type { get; set; }
    }
}
