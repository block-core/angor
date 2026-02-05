using System.IO;
using System.Threading;
using Angor.Shared.Models;

namespace AngorApp.UI.Shared.Services;

/// <summary>
/// Service interface for uploading images to Nostr image servers.
/// </summary>
public interface IImageUploadService
{
    /// <summary>
    /// Gets the list of available image servers.
    /// </summary>
    IReadOnlyList<ImageServerConfig> GetServers();

    /// <summary>
    /// Uploads an image file to the specified server.
    /// </summary>
    /// <param name="server">The server configuration to upload to.</param>
    /// <param name="fileStream">The image file stream.</param>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="contentType">The MIME content type of the file.</param>
    /// <param name="customUploadUrl">Optional custom upload URL for custom servers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the uploaded image URL on success.</returns>
    Task<Result<string>> UploadImageAsync(
        ImageServerConfig server,
        Stream fileStream,
        string fileName,
        string contentType,
        string? customUploadUrl = null,
        CancellationToken cancellationToken = default);
}
