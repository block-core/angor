using System.Net.Http;
using System.Net.Mail;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace AngorApp.UI.Services;

public static class ValidationShared
{
    // Parse email-like addresses (name@domain)
    public static Result<MailAddress> GetAddress(string address)
        => MailAddress.TryCreate(address, out var result)
            ? Result.Success(result)
            : Result.Failure<MailAddress>("Cannot parse address (expected name@domain)");

    // HTTP JSON downloader
    public static async Task<string> JsonFrom(IHttpClientFactory httpClientFactory, Uri uri)
    {
        using var httpClient = httpClientFactory.CreateClient();
        return await httpClient.GetStringAsync(uri);
    }
}