using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Zafiro;

namespace AngorApp.UI.Services;

public class Validations
{
    private readonly IHttpClientFactory httpClientFactory;

    public Validations(IHttpClientFactory httpClientFactory)
    {
        this.httpClientFactory = httpClientFactory;
    }
    
    public Task<Result<bool>> IsValidNip05Username(string username, string nostrPubKey)
    {
        var isValidNip05Username = Result.Success()
            .Map(() => MailAddress.TryCreate(username, out var mailAddress) ? mailAddress : null)
            .Bind(address => Result
                .Try(() => JsonFrom(new Uri($"https://{address!.Host}/.well-known/nostr.json?name={address.User}")))
                .Map(document => (document, address)))
            .Bind(x =>
            {
                Result<(JsonElement, MailAddress)> success;
                if (x.document.RootElement.TryGetProperty("names", out var names))
                {
                    success = Result.Success<(JsonElement element, MailAddress address)>((names, x.address));
                }
                else
                {
                    success = Result.Failure<(JsonElement, MailAddress)>("Names not found");
                }

                return success;
            })
            .Bind(x =>
            {
                Result<(JsonElement, MailAddress)> success;
                if (x.Item1.TryGetProperty(x.Item2.User, out var user))
                {
                    success = Result.Success<(JsonElement element, MailAddress address)>((user, x.Item2));
                }
                else
                {
                    success = Result.Failure<(JsonElement, MailAddress)>("User not found");
                }

                return success;
            })
            .Map(tuple => tuple.Item1.GetString() == nostrPubKey);
            
        
        return isValidNip05Username;
    }

    private async Task<JsonDocument> JsonFrom(Uri uri)
    {
        using (var httpClient = httpClientFactory.CreateClient())
        {
            await using (var stream = await httpClient.GetStreamAsync(uri))
            {
                return await JsonDocument.ParseAsync(stream);
            }
        }
    }

    public Task<Result<bool>> IsValidLightningAddress(string address)
    {
        throw new NotImplementedException();
    }
}