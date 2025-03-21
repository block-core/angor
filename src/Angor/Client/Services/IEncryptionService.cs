namespace Angor.Client.Services
{
    public interface IEncryptionService
    {
        Task<string> EncryptData(string secretData, string password);
        Task<string> DecryptData(string encryptedData, string password);
    }

}
