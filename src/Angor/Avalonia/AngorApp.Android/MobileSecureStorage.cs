using System;
using System.Linq;
using System.Text;
using Android.Content;
using Android.Security.Keystore;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace AngorApp.Android;

public class MobileSecureStorage : ISecureStorage
{
    private const string KeyAlias = "secure_storage_key";
    private readonly ISharedPreferences _prefs;

    public MobileSecureStorage(Context context)
    {
        _prefs = context.GetSharedPreferences("secure_storage", FileCreationMode.Private);
        EnsureKey();
    }

    private void EnsureKey()
    {
        var keyStore = KeyStore.GetInstance("AndroidKeyStore");
        keyStore.Load(null);
        if (!keyStore.ContainsAlias(KeyAlias))
        {
            var builder = new KeyGenParameterSpec.Builder(KeyAlias, KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
                .SetBlockModes(KeyProperties.BlockModeCbc)
                .SetEncryptionPaddings(KeyProperties.EncryptionPaddingPkcs7)
                .SetRandomizedEncryptionRequired(true);

            var keyGenerator = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, "AndroidKeyStore");
            keyGenerator.Init(builder.Build());
            keyGenerator.GenerateKey();
        }
    }

    private ISecretKey GetSecretKey()
    {
        var keyStore = KeyStore.GetInstance("AndroidKeyStore");
        keyStore.Load(null);
        return (ISecretKey)keyStore.GetKey(KeyAlias, null);
    }

    public Result<string> Encrypt(string plainText)
    {
        try
        {
            var cipher = Cipher.GetInstance("AES/CBC/PKCS7Padding");
            cipher.Init(CipherMode.EncryptMode, GetSecretKey());
            var iv = cipher.GetIV();
            var encrypted = cipher.DoFinal(Encoding.UTF8.GetBytes(plainText));
            // Ensure combined is a byte[]
            var combined = new byte[iv.Length + encrypted.Length];
            Buffer.BlockCopy(iv, 0, combined, 0, iv.Length);
            Buffer.BlockCopy(encrypted, 0, combined, iv.Length, encrypted.Length);
            var base64 = Convert.ToBase64String(combined);
            _prefs.Edit().PutString("data", base64).Apply();
            return Result.Success(base64);
        }
        catch (Exception ex)
        {
            return Result.Failure<string>(ex.Message);
        }
    }


    public Result<string> Decrypt(string cipherText)
    {
        try
        {
            var base64 = _prefs.GetString("data", null);
            if (string.IsNullOrEmpty(base64))
                return Result.Failure<string>("No data found");

            var allBytes = Convert.FromBase64String(base64);
            var iv = allBytes.Take(16).ToArray();
            var encrypted = allBytes.Skip(16).ToArray();

            var cipher = Cipher.GetInstance("AES/CBC/PKCS7Padding");
            var spec = new IvParameterSpec(iv);
            cipher.Init(CipherMode.DecryptMode, GetSecretKey(), spec);
            var decrypted = cipher.DoFinal(encrypted);
            return Result.Success(Encoding.UTF8.GetString(decrypted));
        }
        catch (Exception ex)
        {
            return Result.Failure<string>(ex.Message);
        }
    }
}
