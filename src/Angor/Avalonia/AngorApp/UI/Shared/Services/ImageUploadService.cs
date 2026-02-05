using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using Angor.Shared.Models;
using Serilog;

namespace AngorApp.UI.Shared.Services;

/// <summary>
/// Service for uploading images to Nostr image servers.
/// </summary>
public sealed class ImageUploadService : IImageUploadService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;
    private const long MaxFileSize = 5 * 1024 * 1024; // 5MB

    public ImageUploadService(IHttpClientFactory httpClientFactory, ILogger logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IReadOnlyList<ImageServerConfig> GetServers()
    {
        return ImageServerConfig.GetDefaultServers().AsReadOnly();
    }

    public async Task<Result<string>> UploadImageAsync(
        ImageServerConfig server,
        Stream fileStream,
        string fileName,
        string contentType,
        string? customUploadUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (server == null)
            return Result.Failure<string>("Server configuration is required.");

        if (fileStream == null)
            return Result.Failure<string>("File stream is required.");

        if (string.IsNullOrWhiteSpace(fileName))
            return Result.Failure<string>("File name is required.");

        if (string.IsNullOrWhiteSpace(contentType))
            return Result.Failure<string>("Content type is required.");

        var uploadUrl = server.IsCustom ? customUploadUrl : server.UploadUrl;
        if (string.IsNullOrWhiteSpace(uploadUrl))
            return Result.Failure<string>("Upload URL is required.");

        if (fileStream.Length > MaxFileSize)
            return Result.Failure<string>($"File size exceeds {FormatFileSize(MaxFileSize)} limit.");

        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            using var content = new MultipartFormDataContent();
            using var memoryStream = new MemoryStream();
            
            await fileStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;
            
            var fileContent = new StreamContent(memoryStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            // Different servers use different field names
            // "file" is a common field name used by most servers
            content.Add(fileContent, "file", fileName);

            _logger.Information("Uploading image to {ServerName} ({UploadUrl})", server.Name, uploadUrl);

            using var response = await httpClient.PostAsync(uploadUrl, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var uploadedUrl = ExtractImageUrlFromResponse(responseContent, server.Name);

                if (!string.IsNullOrEmpty(uploadedUrl))
                {
                    _logger.Information("Image uploaded successfully to {ServerName}. URL: {ImageUrl}", server.Name, uploadedUrl);
                    return Result.Success(uploadedUrl);
                }
                else
                {
                    _logger.Warning("Upload succeeded but could not parse image URL from response. Response: {Response}", responseContent);
                    return Result.Failure<string>("Upload succeeded but could not parse image URL from response. Please check the server's API documentation.");
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.Warning("Upload failed with status {StatusCode}. Response: {Response}", response.StatusCode, errorContent);
                
                var errorMessage = response.StatusCode == System.Net.HttpStatusCode.Forbidden
                    ? $"Upload failed: {response.ReasonPhrase}. This may be an authentication requirement."
                    : $"Upload failed: {response.ReasonPhrase}";
                
                return Result.Failure<string>(errorMessage);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.Error(ex, "HTTP error during image upload to {ServerName}", server.Name);
            return Result.Failure<string>($"Upload error: {ex.Message}. This may be a network connectivity issue.");
        }
        catch (OperationCanceledException)
        {
            _logger.Information("Image upload to {ServerName} was cancelled", server.Name);
            return Result.Failure<string>("Upload was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unexpected error during image upload to {ServerName}", server.Name);
            return Result.Failure<string>($"Upload error: {ex.Message}");
        }
    }

    private static string ExtractImageUrlFromResponse(string response, string serverName)
    {
        try
        {
            // Try to find a URL in the response that looks like an image
            var urlPattern = @"https?://[^\s""'<>]+\.(jpg|jpeg|png|gif|webp|svg)";
            var match = Regex.Match(response, urlPattern, RegexOptions.IgnoreCase);

            if (match.Success)
            {
                return match.Value;
            }

            // Some APIs might return JSON with a url field
            // Try to extract url from common JSON patterns
            var jsonUrlPatterns = new[]
            {
                @"""url""\s*:\s*""([^""]+)""",
                @"""image_url""\s*:\s*""([^""]+)""",
                @"""link""\s*:\s*""([^""]+)""",
                @"""data""\s*:\s*\{[^}]*""url""\s*:\s*""([^""]+)""",
            };

            foreach (var pattern in jsonUrlPatterns)
            {
                match = Regex.Match(response, pattern, RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value;
                }
            }

            // If no URL found, return empty string
            return string.Empty;
        }
        catch (Exception)
        {
            // Regex parsing failed - response format may be unexpected
            return string.Empty;
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
