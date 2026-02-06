using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using CSharpFunctionalExtensions;
using Serilog;

namespace AngorApp.UI.Shared.Services.Blossom;

/// <summary>
/// Implements Blossom BUD-02 blob upload via PUT /upload.
/// The server receives raw binary in the body with Content-Type header.
/// Returns a BlobDescriptor JSON with the download URL.
/// </summary>
public class BlossomService : IBlossomService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public async Task<Result<BlobDescriptor>> Upload(string serverBaseUrl, byte[] fileBytes, string contentType, CancellationToken cancellationToken = default)
    {
        try
        {
            var baseUrl = serverBaseUrl.TrimEnd('/');
            var uploadUrl = $"{baseUrl}/upload";

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(5);

            using var content = new ByteArrayContent(fileBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            Log.Information("Uploading {Size} bytes to Blossom server {Url}", fileBytes.Length, uploadUrl);

            using var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl)
            {
                Content = content
            };

            using var response = await client.SendAsync(request, cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var reason = response.Headers.Contains("X-Reason")
                    ? response.Headers.GetValues("X-Reason").FirstOrDefault()
                    : responseBody;

                Log.Warning("Blossom upload failed with status {StatusCode}: {Reason}", response.StatusCode, reason);
                return Result.Failure<BlobDescriptor>($"Upload failed ({response.StatusCode}): {reason}");
            }

            var descriptor = JsonSerializer.Deserialize<BlobDescriptorResponse>(responseBody, JsonOptions);

            if (descriptor?.Url is null || descriptor.Sha256 is null)
            {
                Log.Warning("Blossom upload returned invalid response: {Response}", responseBody);
                return Result.Failure<BlobDescriptor>("Server returned an invalid response");
            }

            Log.Information("Blossom upload successful: {Url}", descriptor.Url);
            return new BlobDescriptor(descriptor.Url, descriptor.Sha256, descriptor.Size, descriptor.Type ?? contentType);
        }
        catch (TaskCanceledException)
        {
            return Result.Failure<BlobDescriptor>("Upload was cancelled");
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "Network error during Blossom upload");
            return Result.Failure<BlobDescriptor>($"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error during Blossom upload");
            return Result.Failure<BlobDescriptor>($"Upload error: {ex.Message}");
        }
    }

    private class BlobDescriptorResponse
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("sha256")]
        public string? Sha256 { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }
}
