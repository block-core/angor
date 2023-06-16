namespace Angor.Client.Services
{
    public interface IClipboardService
    {
        Task<string> ReadTextAsync();
        Task WriteTextAsync(string text);
    }

}
