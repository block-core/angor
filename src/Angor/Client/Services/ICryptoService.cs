namespace Angor.Client.Services;

public interface ICryptoService
{
    Task<string> EncryptDataAsync(string password, string secretData);

    Task<string> DecryptDataAsync(string password, string encryptedData);
}