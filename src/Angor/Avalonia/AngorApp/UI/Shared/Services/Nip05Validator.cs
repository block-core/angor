using System.Net.Http;
using System.Net.Mail;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace AngorApp.UI.Shared.Services;

public class Nip05Validator(IHttpClientFactory httpClientFactory)
{
    public async Task<Result> CheckNip05Username(string username, string nostrPubKey)
    {
        var valid =
            from address in ValidationShared.GetAddress(username)
            from serverNpub in GetServerNpub(address)
            select serverNpub;

        return await valid.Ensure(serverNpub => serverNpub == nostrPubKey, "Found Npub does not match your project's Nostr pubkey");
    }

    private async Task<Result<string>> GetServerNpub(MailAddress address)
    {
        return await Result
            .Try(() => ValidationShared.JsonFrom(
                httpClientFactory,
                new Uri($"https://{address.Host}/.well-known/nostr.json?name={address.User}")))
            .Map(token => JToken.Parse(token).SelectToken($"$.names.{address.User}"))
            .EnsureNotNull($"Cannot find Npub for username '{address.User}'")
            .Map(token => token.Value<string>())
            .EnsureNotNull($"Found Npub for {address.User}, but it is null");
    }
}