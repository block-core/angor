using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Android.Security.Keystore;
using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;

namespace App.Android;

/// <summary>
/// Android ISecureKeyProvider backed by the Android KeyStore. A master AES key is
/// generated inside the hardware-backed KeyStore and used to encrypt/decrypt the
/// per-wallet encryption keys stored on disk.
/// </summary>
public class AndroidKeyStoreSecureKeyProvider : ISecureKeyProvider
{
    private const string KeyStoreAlias = "AngorWalletMasterKey";
    private const string KeyStoreName = "AndroidKeyStore";
    private const string Transformation = "AES/GCM/NoPadding";
    private const int GcmTagLength = 128;
    private readonly string _filePath;

    public AndroidKeyStoreSecureKeyProvider(IApplicationStorage storage, ProfileContext profileContext)
    {
        var directory = storage.GetProfileDirectory(profileContext.AppName, profileContext.ProfileName);
        _filePath = Path.Combine(directory, "wallet-keys.enc");
        EnsureMasterKey();
    }

    public Task<Maybe<string>> Get(WalletId walletId)
    {
        var keys = LoadKeys();
        return Task.FromResult(keys.TryGetValue(walletId.Value, out var key)
            ? Maybe<string>.From(key)
            : Maybe<string>.None);
    }

    public Task Save(WalletId walletId, string key)
    {
        var keys = LoadKeys();
        keys[walletId.Value] = key;
        SaveKeys(keys);
        return Task.CompletedTask;
    }

    public Task Remove(WalletId walletId)
    {
        var keys = LoadKeys();
        if (keys.Remove(walletId.Value))
        {
            SaveKeys(keys);
        }
        return Task.CompletedTask;
    }

    public string GenerateKey()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static void EnsureMasterKey()
    {
        var keyStore = KeyStore.GetInstance(KeyStoreName)!;
        keyStore.Load(null);

        if (keyStore.ContainsAlias(KeyStoreAlias))
            return;

        var keyGenerator = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, KeyStoreName)!;
        var spec = new KeyGenParameterSpec.Builder(KeyStoreAlias, KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
            .SetBlockModes(KeyProperties.BlockModeGcm)!
            .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)!
            .SetKeySize(256)!
            .Build();

        keyGenerator.Init(spec);
        keyGenerator.GenerateKey();
    }

    private static IKey GetMasterKey()
    {
        var keyStore = KeyStore.GetInstance(KeyStoreName)!;
        keyStore.Load(null);
        return keyStore.GetKey(KeyStoreAlias, null)!;
    }

    private static byte[] Encrypt(byte[] plaintext)
    {
        var cipher = Cipher.GetInstance(Transformation)!;
        cipher.Init(Javax.Crypto.CipherMode.EncryptMode, GetMasterKey());
        var iv = cipher.GetIV()!;
        var encrypted = cipher.DoFinal(plaintext)!;

        // Prefix IV length (1 byte) + IV + ciphertext
        var result = new byte[1 + iv.Length + encrypted.Length];
        result[0] = (byte)iv.Length;
        Buffer.BlockCopy(iv, 0, result, 1, iv.Length);
        Buffer.BlockCopy(encrypted, 0, result, 1 + iv.Length, encrypted.Length);
        return result;
    }

    private static byte[] Decrypt(byte[] data)
    {
        var ivLength = data[0];
        var iv = new byte[ivLength];
        Buffer.BlockCopy(data, 1, iv, 0, ivLength);
        var encrypted = new byte[data.Length - 1 - ivLength];
        Buffer.BlockCopy(data, 1 + ivLength, encrypted, 0, encrypted.Length);

        var cipher = Cipher.GetInstance(Transformation)!;
        cipher.Init(Javax.Crypto.CipherMode.DecryptMode, GetMasterKey(), new GCMParameterSpec(GcmTagLength, iv));
        return cipher.DoFinal(encrypted)!;
    }

    private Dictionary<string, string> LoadKeys()
    {
        if (!File.Exists(_filePath))
            return new Dictionary<string, string>();

        var encryptedBytes = File.ReadAllBytes(_filePath);
        if (encryptedBytes.Length == 0)
            return new Dictionary<string, string>();

        var decrypted = Decrypt(encryptedBytes);
        var json = Encoding.UTF8.GetString(decrypted);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
    }

    private void SaveKeys(Dictionary<string, string> keys)
    {
        var json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(keys));
        var encrypted = Encrypt(json);
        File.WriteAllBytes(_filePath, encrypted);
    }
}
