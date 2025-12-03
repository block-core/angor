using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Security.Keystore;
using Angor.Contexts.CrossCutting;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;
using CipherMode = Javax.Crypto.CipherMode;

namespace AngorApp.Android.PasswordStore;

public class AndroidAutoPasswordStore : IAutoPasswordStore
{
    private const string AndroidKeyStore = "AndroidKeyStore";
    private const string MasterKeyAlias = "AngorAutoPasswordMasterKey";
    private const string PrefsName = "angor_auto_passwords";

    private readonly ISharedPreferences _prefs;

    public AndroidAutoPasswordStore()
    {
        var context = Application.Context;
        _prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private);
        EnsureMasterKey();
    }

    public async Task<string> GetOrCreatePasswordAsync(WalletId walletId)
    {
        var existing = await GetPasswordAsync(walletId);
        if (existing != null)
            return existing;

        var newPassword = GeneratePassword();
        await SavePasswordAsync(walletId, newPassword);
        return newPassword;
    }

    public Task<string?> GetPasswordAsync(WalletId walletId)
    {
        var base64 = _prefs.GetString($"pwd_{walletId.Value}", null);
        if (string.IsNullOrEmpty(base64))
            return Task.FromResult<string?>(null);

        var encryptedBytes = Convert.FromBase64String(base64);
        var plainBytes = DecryptWithMasterKey(encryptedBytes);
        var value = Encoding.UTF8.GetString(plainBytes);
        return Task.FromResult<string?>(value);
    }

    public Task DeletePasswordAsync(WalletId walletId)
    {
        using var editor = _prefs.Edit();
        editor.Remove($"pwd_{walletId.Value}");
        editor.Commit();
        return Task.CompletedTask;
    }

    private Task SavePasswordAsync(WalletId walletId, string password)
    {
        var plainBytes = Encoding.UTF8.GetBytes(password);
        var encrypted = EncryptWithMasterKey(plainBytes);
        var base64 = Convert.ToBase64String(encrypted);

        using var editor = _prefs.Edit();
        editor.PutString($"pwd_{walletId.Value}", base64);
        editor.Commit();

        return Task.CompletedTask;
    }

    private void EnsureMasterKey()
    {
        var keyStore = KeyStore.GetInstance(AndroidKeyStore);
        keyStore.Load(null);

        if (!keyStore.IsKeyEntry(MasterKeyAlias))
        {
            var generator = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, AndroidKeyStore);
            var builder = new KeyGenParameterSpec.Builder(
                    MasterKeyAlias,
                    KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
                .SetBlockModes(KeyProperties.BlockModeGcm)
                .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)
                .SetRandomizedEncryptionRequired(true);

            generator.Init(builder.Build());
            generator.GenerateKey();
        }
    }

    private static IKey? GetMasterKey()
    {
        var keyStore = KeyStore.GetInstance(AndroidKeyStore);
        keyStore.Load(null);
        return keyStore.GetKey(MasterKeyAlias, null);
    }

    private static byte[] EncryptWithMasterKey(byte[] data)
    {
        var key = GetMasterKey();
        var cipher = Cipher.GetInstance("AES/GCM/NoPadding");
        cipher.Init(CipherMode.EncryptMode, key);
        var iv = cipher.GetIV();
        var cipherText = cipher.DoFinal(data);

        var result = new byte[iv.Length + cipherText.Length];
        Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
        Buffer.BlockCopy(cipherText, 0, result, iv.Length, cipherText.Length);
        return result;
    }

    private static byte[] DecryptWithMasterKey(byte[] input)
    {
        var key = GetMasterKey();
        var iv = input.Take(12).ToArray();
        var cipherText = input.Skip(12).ToArray();

        var cipher = Cipher.GetInstance("AES/GCM/NoPadding");
        var spec = new GCMParameterSpec(128, iv);

        cipher.Init(CipherMode.DecryptMode, key, spec);
        return cipher.DoFinal(cipherText);
    }

    private static string GeneratePassword()
    {
        const int keySizeBytes = 32; // 256 bits
        var bytes = RandomNumberGenerator.GetBytes(keySizeBytes);
        return Convert.ToBase64String(bytes);
    }
}

