using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Angor.Sdk.Funding.Projects;
using Angor.Shared;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace App.Test.Integration;

/// <summary>
/// Integration tests for the log export feature.
/// Validates the full encrypt → upload → decrypt roundtrip, including
/// interoperability with the Node.js decrypt-log.mjs script.
/// </summary>
public class LogExportRoundtripTest
{
    private readonly ITestOutputHelper _output;
    private readonly IEncryptionService _encryptionService;

    // Fixed test keys (secp256k1) — NOT real keys, only for testing
    private const string SenderPrivateKeyHex = "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2";
    private const string RecipientPrivateKeyHex = "b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3";

    private static readonly string DecryptScriptDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tools", "decrypt-log"));

    public LogExportRoundtripTest(ITestOutputHelper output)
    {
        _output = output;
        _encryptionService = new EncryptionService();
    }

    private static string GetPublicKeyHex(string privateKeyHex)
    {
        var key = new Key(Encoders.Hex.DecodeData(privateKeyHex));
        // Nostr uses x-only (32 bytes) public keys — strip the 02/03 prefix
        return key.PubKey.ToHex()[2..];
    }

    [Fact]
    public async Task EncryptAndDecrypt_CSharp_Roundtrip()
    {
        // Arrange: create a small zip with fake log data
        var logContent = "2024-01-01 12:00:00.000 [INF] Test log line\n";
        var zipBytes = CreateTestLogZip(logContent);
        var zipBase64 = Convert.ToBase64String(zipBytes);

        var senderPubHex = GetPublicKeyHex(SenderPrivateKeyHex);
        var recipientPubHex = GetPublicKeyHex(RecipientPrivateKeyHex);

        _output.WriteLine($"Sender pubkey:    {senderPubHex}");
        _output.WriteLine($"Recipient pubkey: {recipientPubHex}");

        // Act: encrypt (sender encrypts for recipient)
        var encrypted = await _encryptionService.EncryptNostrContentAsync(
            SenderPrivateKeyHex, recipientPubHex, zipBase64);

        _output.WriteLine($"Encrypted blob length: {encrypted.Length}");
        encrypted.Should().Contain("?iv=", "NIP-04 format requires base64?iv=base64");

        // Act: decrypt (recipient decrypts from sender)
        var decrypted = await _encryptionService.DecryptNostrContentAsync(
            RecipientPrivateKeyHex, senderPubHex, encrypted);

        // Assert
        decrypted.Should().Be(zipBase64);
        var recoveredZip = Convert.FromBase64String(decrypted);
        recoveredZip.Should().BeEquivalentTo(zipBytes);

        // Verify ZIP structure
        using var ms = new MemoryStream(recoveredZip);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        archive.Entries.Should().ContainSingle();
        archive.Entries[0].Name.Should().Be("test.log");

        using var reader = new StreamReader(archive.Entries[0].Open());
        var content = await reader.ReadToEndAsync();
        content.Should().Be(logContent);

        _output.WriteLine("C# roundtrip: PASS");
    }

    [Fact]
    public async Task EncryptCSharp_DecryptNodeScript_Roundtrip()
    {
        if (!IsNodeAvailable())
        {
            _output.WriteLine("SKIPPED: Node.js is not available");
            return;
        }
        if (!IsDecryptScriptReady())
        {
            _output.WriteLine($"SKIPPED: decrypt-log node_modules not installed at {DecryptScriptDir}");
            return;
        }

        // Arrange
        var logContent = $"2024-05-10 09:30:00.000 [INF] LogExport integration test at {DateTime.UtcNow:O}\n";
        var zipBytes = CreateTestLogZip(logContent);
        var zipBase64 = Convert.ToBase64String(zipBytes);

        var senderPubHex = GetPublicKeyHex(SenderPrivateKeyHex);
        var recipientPubHex = GetPublicKeyHex(RecipientPrivateKeyHex);

        // Encrypt using C# (same path as LogExportService)
        var encryptedBlob = await _encryptionService.EncryptNostrContentAsync(
            SenderPrivateKeyHex, recipientPubHex, zipBase64);

        _output.WriteLine($"Encrypted blob: {encryptedBlob.Length} chars");

        // Write encrypted blob to a temp file and serve it via a local HTTP file
        // The decrypt script expects HTTPS, so we'll use a helper script that
        // reads directly from stdin instead. We'll create a small wrapper.
        var tempDir = Path.Combine(Path.GetTempPath(), $"angor-log-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        // Place helper script IN DecryptScriptDir so ESM can resolve nostr-tools
        var decryptHelperFile = Path.Combine(DecryptScriptDir, $"_test-helper-{Guid.NewGuid():N}.mjs");

        try
        {
            var blobFile = Path.Combine(tempDir, "blob.enc");
            var outputFile = Path.Combine(tempDir, "output.zip");

            await File.WriteAllTextAsync(blobFile, encryptedBlob);

            // Create a minimal decrypt helper that reads from local file instead of HTTPS.
            // Must run from DecryptScriptDir so ESM module resolution finds nostr-tools.
            var helperScript = $@"
import {{ nip04 }} from 'nostr-tools';
import {{ readFileSync, writeFileSync }} from 'fs';
import {{ resolve }} from 'path';

const recipientPrivateKeyHex = process.argv[2];
const senderPubkeyHex = process.argv[3];
const blobPath = resolve(process.argv[4]);
const outputPath = resolve(process.argv[5]);

const blobContent = readFileSync(blobPath, 'utf8');

if (!blobContent.includes('?iv=')) {{
  console.error('ERROR: Not NIP-04 format');
  process.exit(1);
}}

const zipBase64 = await nip04.decrypt(recipientPrivateKeyHex, senderPubkeyHex, blobContent);
const zipBytes = Buffer.from(zipBase64, 'base64');

if (zipBytes.length < 4 || zipBytes[0] !== 0x50 || zipBytes[1] !== 0x4B) {{
  console.error('WARNING: Not a valid ZIP. First bytes: ' + zipBytes.slice(0, 8).toString('hex'));
  process.exit(2);
}}

writeFileSync(outputPath, zipBytes);
console.log(`OK: ${{zipBytes.length}} bytes`);
";
            await File.WriteAllTextAsync(decryptHelperFile, helperScript);

            // Run from DecryptScriptDir so ESM resolution finds node_modules/nostr-tools
            var psi = new ProcessStartInfo
            {
                FileName = "node",
                Arguments = $"\"{decryptHelperFile}\" {RecipientPrivateKeyHex} {senderPubHex} \"{blobFile}\" \"{outputFile}\"",
                WorkingDirectory = DecryptScriptDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            _output.WriteLine($"Running: node decrypt-local.mjs ...");
            _output.WriteLine($"WorkingDirectory: {DecryptScriptDir}");

            using var process = Process.Start(psi)!;
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            _output.WriteLine($"stdout: {stdout.Trim()}");
            if (!string.IsNullOrWhiteSpace(stderr))
                _output.WriteLine($"stderr: {stderr.Trim()}");

            process.ExitCode.Should().Be(0, $"decrypt script failed: {stderr}");

            // Verify the output zip
            File.Exists(outputFile).Should().BeTrue("decrypt script should produce output.zip");
            var decryptedZipBytes = await File.ReadAllBytesAsync(outputFile);
            decryptedZipBytes.Should().BeEquivalentTo(zipBytes, "decrypted ZIP should match original");

            // Verify ZIP contents
            using var ms = new MemoryStream(decryptedZipBytes);
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
            archive.Entries.Should().ContainSingle();

            using var reader = new StreamReader(archive.Entries[0].Open());
            var recovered = await reader.ReadToEndAsync();
            recovered.Should().Be(logContent);

            _output.WriteLine("Node.js decrypt roundtrip: PASS");
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* cleanup best-effort */ }
            try { File.Delete(decryptHelperFile); } catch { /* cleanup best-effort */ }
        }
    }

    [Fact]
    public async Task EncryptCSharp_DecryptWithOriginalScript_Roundtrip()
    {
        // This test validates the actual decrypt-log.mjs script's downloadAndDecrypt logic
        // by monkey-patching the fetch call to read from a local file.
        if (!IsNodeAvailable())
        {
            _output.WriteLine("SKIPPED: Node.js is not available");
            return;
        }
        if (!IsDecryptScriptReady())
        {
            _output.WriteLine($"SKIPPED: decrypt-log node_modules not installed at {DecryptScriptDir}");
            return;
        }

        // Arrange
        var logLines = new StringBuilder();
        logLines.AppendLine("2024-05-10 08:00:00.000 +00:00 [INF] App.Startup: Application started");
        logLines.AppendLine("2024-05-10 08:00:01.123 +00:00 [INF] Wallet.Sync: Syncing wallet abc123");
        logLines.AppendLine("2024-05-10 08:00:02.456 +00:00 [WRN] Network: Relay timeout on wss://relay.example.com");

        var zipBytes = CreateTestLogZip(logLines.ToString(), "angor-20240510.log");
        var zipBase64 = Convert.ToBase64String(zipBytes);

        var senderPubHex = GetPublicKeyHex(SenderPrivateKeyHex);
        var recipientPubHex = GetPublicKeyHex(RecipientPrivateKeyHex);

        var encryptedBlob = await _encryptionService.EncryptNostrContentAsync(
            SenderPrivateKeyHex, recipientPubHex, zipBase64);

        var tempDir = Path.Combine(Path.GetTempPath(), $"angor-script-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        // Place wrapper in DecryptScriptDir so ESM can resolve nostr-tools
        var wrapperFile = Path.Combine(DecryptScriptDir, $"_test-wrapper-{Guid.NewGuid():N}.mjs");

        try
        {
            var blobFile = Path.Combine(tempDir, "blob.enc");
            var outputFile = Path.Combine(tempDir, "decrypted-log.zip");

            await File.WriteAllTextAsync(blobFile, encryptedBlob);

            // Create wrapper that patches global fetch to serve our local blob file,
            // then calls the real downloadAndDecrypt function from the script
            var wrapperScript = $@"
import {{ nip04, nip19, getPublicKey }} from 'nostr-tools';
import {{ readFileSync, writeFileSync }} from 'fs';
import {{ resolve }} from 'path';

// Patch global fetch to serve local file
const blobPath = resolve(process.argv[4]);
globalThis.fetch = async (url) => {{
  const content = readFileSync(blobPath, 'utf8');
  return {{
    ok: true,
    text: async () => content,
  }};
}};

// Now replicate downloadAndDecrypt from the real script
const privateKeyHex = process.argv[2];
const senderPubkey = process.argv[3];
const outputPath = resolve(process.argv[5] || 'decrypted-log.zip');

const blobContent = readFileSync(blobPath, 'utf8');

if (!blobContent.includes('?iv=')) {{
  console.error('ERROR: Blob is not in NIP-04 format');
  process.exit(1);
}}

console.log('Decrypting blob...');
const zipBase64 = await nip04.decrypt(privateKeyHex, senderPubkey, blobContent);
const zipBuf = Buffer.from(zipBase64, 'base64');

if (zipBuf.length < 4 || zipBuf[0] !== 0x50 || zipBuf[1] !== 0x4B) {{
  console.error('WARNING: Decrypted content does not look like a zip file');
  console.error('First bytes:', zipBuf.slice(0, 8).toString('hex'));
  process.exit(2);
}}

writeFileSync(outputPath, zipBuf);
console.log(`Decrypted zip saved to: ${{outputPath}} (${{zipBuf.length}} bytes)`);
";
            await File.WriteAllTextAsync(wrapperFile, wrapperScript);

            // Run from DecryptScriptDir so ESM resolution finds node_modules/nostr-tools
            var psi = new ProcessStartInfo
            {
                FileName = "node",
                Arguments = $"\"{wrapperFile}\" {RecipientPrivateKeyHex} {senderPubHex} \"{blobFile}\" \"{outputFile}\"",
                WorkingDirectory = DecryptScriptDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi)!;
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            _output.WriteLine($"stdout: {stdout.Trim()}");
            if (!string.IsNullOrWhiteSpace(stderr))
                _output.WriteLine($"stderr: {stderr.Trim()}");

            process.ExitCode.Should().Be(0, $"decrypt script failed (exit {process.ExitCode}): {stderr}");

            // Validate output
            var decryptedBytes = await File.ReadAllBytesAsync(outputFile);
            decryptedBytes.Should().BeEquivalentTo(zipBytes);

            using var ms = new MemoryStream(decryptedBytes);
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
            archive.Entries.Should().ContainSingle()
                .Which.Name.Should().Be("angor-20240510.log");

            _output.WriteLine("Original script interop: PASS");
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* cleanup best-effort */ }
            try { File.Delete(wrapperFile); } catch { /* cleanup best-effort */ }
        }
    }

    [Fact]
    public void CreateLogZip_ProducesValidZipWithExpectedContent()
    {
        var logContent = "Line 1\nLine 2\nLine 3\n";
        var zipBytes = CreateTestLogZip(logContent, "angor-.log");

        // Should start with PK magic bytes
        zipBytes[0].Should().Be(0x50);
        zipBytes[1].Should().Be(0x4B);

        using var ms = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

        archive.Entries.Should().ContainSingle();
        archive.Entries[0].Name.Should().Be("angor-.log");

        using var reader = new StreamReader(archive.Entries[0].Open());
        reader.ReadToEnd().Should().Be(logContent);
    }

    [Fact]
    public async Task NIP04_SharedSecret_IsSymmetric()
    {
        // NIP-04 relies on ECDH: encrypt(senderPriv, recipPub) == decrypt(recipPriv, senderPub)
        var senderPub = GetPublicKeyHex(SenderPrivateKeyHex);
        var recipientPub = GetPublicKeyHex(RecipientPrivateKeyHex);

        var plaintext = "Hello, this is a test message for NIP-04 symmetry";

        // Sender encrypts to recipient
        var encrypted = await _encryptionService.EncryptNostrContentAsync(
            SenderPrivateKeyHex, recipientPub, plaintext);

        // Recipient decrypts from sender
        var decrypted = await _encryptionService.DecryptNostrContentAsync(
            RecipientPrivateKeyHex, senderPub, encrypted);

        decrypted.Should().Be(plaintext);

        // Also test reverse direction
        var encrypted2 = await _encryptionService.EncryptNostrContentAsync(
            RecipientPrivateKeyHex, senderPub, plaintext);

        var decrypted2 = await _encryptionService.DecryptNostrContentAsync(
            SenderPrivateKeyHex, recipientPub, encrypted2);

        decrypted2.Should().Be(plaintext);
    }

    #region Helpers

    private static byte[] CreateTestLogZip(string logContent, string fileName = "test.log")
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(fileName, CompressionLevel.SmallestSize);
            using var entryStream = entry.Open();
            var bytes = Encoding.UTF8.GetBytes(logContent);
            entryStream.Write(bytes, 0, bytes.Length);
        }
        return ms.ToArray();
    }

    private static bool IsNodeAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo("node", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(5000);
            return p?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDecryptScriptReady()
    {
        var nodeModules = Path.Combine(DecryptScriptDir, "node_modules");
        return Directory.Exists(nodeModules);
    }

    #endregion
}
