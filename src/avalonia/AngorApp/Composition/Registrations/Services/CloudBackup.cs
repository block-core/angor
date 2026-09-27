using Angor.Sdk.WalletExport;
using Angor.Sdk.WalletExport.Blossom;
using Microsoft.Extensions.DependencyInjection;

namespace AngorApp.Composition.Registrations.Services;

/// <summary>
/// Wires the encrypted-seed cloud-backup feature (NIP-44 outer + AES-GCM inner, Argon2id KDF, Blossom blobs).
/// </summary>
public static class CloudBackup
{
    public static IServiceCollection AddCloudBackup(this IServiceCollection services)
    {
        services.AddSingleton<IBackupBlossomClient, BackupBlossomClient>();
        services.AddSingleton<ICloudBackupService, CloudBackupService>();
        services.AddSingleton<IBackupRecoveryService, BackupRecoveryService>();
        services.AddSingleton<IWalletCloudBackupService, WalletCloudBackupService>();
        return services;
    }
}
