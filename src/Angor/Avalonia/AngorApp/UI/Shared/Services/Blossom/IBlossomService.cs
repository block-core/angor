using System.Threading;
using CSharpFunctionalExtensions;

namespace AngorApp.UI.Shared.Services.Blossom;

/// <summary>
/// Service for uploading blobs to Blossom servers per BUD-02.
/// Upload is PUT /upload with raw binary body.
/// Response is a JSON Blob Descriptor with url, sha256, size, type.
/// </summary>
public interface IBlossomService
{
    /// <summary>
    /// Uploads a file to a Blossom-compatible server.
    /// </summary>
    /// <param name="serverBaseUrl">Base URL of the Blossom server (e.g. https://blossom.nostr.hu)</param>
    /// <param name="fileBytes">The file bytes to upload</param>
    /// <param name="contentType">MIME type (e.g. image/png)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The download URL of the uploaded blob</returns>
    Task<Result<BlobDescriptor>> Upload(string serverBaseUrl, byte[] fileBytes, string contentType, CancellationToken cancellationToken = default);
}

public record BlobDescriptor(string Url, string Sha256, long Size, string Type);
