using System;
using System.Linq;
using Angor.Contexts.CrossCutting;
using System.Text;
using System.Threading.Tasks;
using Android.Content;
using Android.App;
using Android.Security.Keystore;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;
using CipherMode = Javax.Crypto.CipherMode;

public class AndroidWalletEncryptionKeyStore : IEncryptionKeyStore
{
    private const string AndroidKeyStore = "AndroidKeyStore";
    private const string MasterKeyAlias = "AngorWalletMasterKey";
    private const string PrefsName = "angor_wallet_keys";

    private readonly ISharedPreferences _prefs;

    public AndroidWalletEncryptionKeyStore()
    {
        var context = Application.Context;
        _prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private);
        EnsureMasterKey();
    }

    private static string BuildKey(WalletId walletId) => $"wallet_encryption_key_{walletId.Value}";

    public Task<string?> GetKeyAsync(WalletId walletId)
    {
        var base64 = _prefs.GetString(BuildKey(walletId), null);
        if (string.IsNullOrEmpty(base64))
            return Task.FromResult<string?>(null);

        var encryptedBytes = Convert.FromBase64String(base64);
        var plainBytes = DecryptWithMasterKey(encryptedBytes);
        var value = Encoding.UTF8.GetString(plainBytes);
        return Task.FromResult<string?>(value);
    }

    public Task SaveKeyAsync(WalletId walletId, string encryptionKey)
    {
        var plainBytes = Encoding.UTF8.GetBytes(encryptionKey);
        var encrypted = EncryptWithMasterKey(plainBytes);
        var base64 = Convert.ToBase64String(encrypted);

        using var editor = _prefs.Edit();
        editor.PutString(BuildKey(walletId), base64);
        editor.Commit();

        return Task.CompletedTask;
    }

    public Task DeleteKeyAsync(WalletId walletId)
    {
        using var editor = _prefs.Edit();
        editor.Remove(BuildKey(walletId));
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
        cipher.Init(CipherMode.EncryptMode, (IKey?)key);
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

        cipher.Init(CipherMode.DecryptMode, (IKey?)key, spec);
        return cipher.DoFinal(cipherText);
    }
}
