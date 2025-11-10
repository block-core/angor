using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;
using System.Security.Cryptography;
using System.Text;

namespace Angor.Contexts.Wallet.Infrastructure.Impl;

public class SimpleSecureStorage : ISecureStorage
{
    public Result<string> Encrypt(string plainText)
    {
        try
        {
            var data = Encoding.UTF8.GetBytes(plainText);
            var encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            return Result.Success(Convert.ToBase64String(encrypted));
        }
        catch (PlatformNotSupportedException pnse)
        {
            return Result.Failure<string>("Encryption is not supported on this platform: " + pnse.Message);
        }
        catch (Exception ex)
        {
            return Result.Failure<string>($"Encryption failed: {ex.Message}");
        }
    }

    public Result<string> Decrypt(string cipherText)
    {
        try
        {
            var encrypted = Convert.FromBase64String(cipherText);
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Result.Success(Encoding.UTF8.GetString(decrypted));
        }
        catch (PlatformNotSupportedException pnse)
        {
            return Result.Failure<string>("Decryption is not supported on this platform: " + pnse.Message);
        }
        catch (Exception ex)
        {
            return Result.Failure<string>($"Decryption failed: {ex.Message}");
        }
    }
}
