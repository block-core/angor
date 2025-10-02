using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Linq;
using Zafiro;

namespace AngorApp.UI.Services;

public class Validations
{
    private readonly IHttpClientFactory httpClientFactory;

    public Validations(IHttpClientFactory httpClientFactory)
    {
        this.httpClientFactory = httpClientFactory;
    }
    
    public async Task<Result> CheckNip05Username(string username, string nostrPubKey)
    {
        var valid = from address in GetAddress(username)
            from serverNpub in GetServerNpub(address)
            select serverNpub;

        return await valid.Ensure(serverNpub => serverNpub == nostrPubKey, "Found Npub does not match your project's Nostr pubkey");
    }

    private Result<MailAddress> GetAddress(string address)
    {
        return MailAddress.TryCreate(address, out var result) ? Result.Success(result) : Result.Failure<MailAddress>("Cannot parse NIP-05 address");
    }

    private Task<Result<string>> GetServerNpub(MailAddress address)
    {
        return Result.Try(() => JsonFrom(new Uri($"https://{address.Host}/.well-known/nostr.json?name={address.User}")))
            .Map(token => JToken.Parse(token).SelectToken($"$.names.{address.User}"))
            .EnsureNotNull($"Cannot find Npub for username '{address.User}'")
            .Map(token => token.Value<string>())
            .EnsureNotNull($"Found Npub for {address.User}, but it is null");
    }

    private async Task<string> JsonFrom(Uri uri)
    {
        using var httpClient = httpClientFactory.CreateClient();
        return await httpClient.GetStringAsync(uri);
    }
}