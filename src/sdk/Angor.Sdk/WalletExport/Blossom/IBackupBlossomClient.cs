using System.Net.Http;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.WalletExport.Blossom;

/// <summary>
/// Blossom client tailored to seed-backup blobs.
/// Uploads with BUD-02 (kind 24242) authorization signed by the passphrase-derived nsec,
/// confirms the server's content-address matches our local SHA-256, and supports
/// existence checks + downloads on recovery.
/// </summary>
public interface IBackupBlossomClient
{
    /// <summary>
    /// PUT the blob to a single Blossom server. Returns the server-confirmed download URL.
    /// </summary>
    Task<Result<BlobUploadResult>> UploadAsync(
        string serverBaseUrl,
        byte[] blob,
        string expectedSha256,
        string nostrPrivateKeyHex,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// HEAD <c>{server}/{sha256}</c> to verify the blob still lives on the server.
    /// </summary>
    Task<Result<bool>> ExistsAsync(string serverBaseUrl, string sha256, CancellationToken cancellationToken = default);

    /// <summary>
    /// GET <c>{server}/{sha256}</c>. Verifies the response body hashes back to the requested sha256
    /// (defends against a malicious or buggy server returning the wrong bytes).
    /// </summary>
    Task<Result<byte[]>> DownloadAsync(string serverBaseUrl, string sha256, CancellationToken cancellationToken = default);
}

public sealed record BlobUploadResult(string ServerBaseUrl, string DownloadUrl, string Sha256, long Size);
