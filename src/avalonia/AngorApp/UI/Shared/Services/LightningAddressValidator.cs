using System.Net.Http;
using System.Net.Mail;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace AngorApp.UI.Shared.Services;

public class LightningAddressValidator(IHttpClientFactory httpClientFactory)
{
    public async Task<Result> CheckLightningAddress(string lightningAddress)
    {
        var result =
            from address in ValidationShared.GetAddress(lightningAddress)
            from json in GetLnurlPayJson(address)
            select ValidateLnurlPayJson(json, address);

        return await result;
    }

    // GET https://{host}/.well-known/lnurlp/{user}
    private async Task<Result<JObject>> GetLnurlPayJson(MailAddress address)
    {
        var uri = new Uri($"https://{address.Host}/.well-known/lnurlp/{address.User}");
        return await Result
            .Try(() => ValidationShared.JsonFrom(httpClientFactory, uri))
            .Map(json => JObject.Parse(json))
            .OnFailureCompensate(e => Result.Failure<JObject>($"Failed to fetch LNURL-Pay JSON: {e}"));
    }

    // Semantic checks required by LNURL-Pay
    private Result ValidateLnurlPayJson(JObject obj, MailAddress address)
    {
        var tag = obj.SelectToken("$.tag")?.Value<string>();
        if (tag != "payRequest")
            return Result.Failure($"Invalid LNURL tag '{tag ?? "null"}' (expected 'payRequest')");

        var callback = obj.SelectToken("$.callback")?.Value<string>();
        if (string.IsNullOrWhiteSpace(callback) ||
            !Uri.TryCreate(callback, UriKind.Absolute, out var cb) ||
            cb.Scheme != Uri.UriSchemeHttps)
        {
            return Result.Failure("Invalid or non-HTTPS callback URL");
        }

        var minSendable = obj.SelectToken("$.minSendable")?.Value<long?>() ?? -1;
        var maxSendable = obj.SelectToken("$.maxSendable")?.Value<long?>() ?? -1;
        if (minSendable <= 0 || maxSendable <= 0 || minSendable > maxSendable)
            return Result.Failure($"Invalid sendable range: min={minSendable}, max={maxSendable}");

        var metadataRaw = obj.SelectToken("$.metadata")?.Value<string>();
        if (string.IsNullOrWhiteSpace(metadataRaw))
            return Result.Failure("Missing metadata");

        var expectedIdentifier = $"{address.User}@{address.Host}";
        var identifierResult = MetadataContainsIdentifier(metadataRaw, expectedIdentifier);
        if (identifierResult.IsFailure || !identifierResult.Value)
            return Result.Failure("Metadata does not confirm the Lightning Address");

        return Result.Success();
    }

    // Inspect LNURL metadata JSON string for ["text/identifier","name@domain"]. Uses Result instead of try/catch.
    private static Result<bool> MetadataContainsIdentifier(string metadataRaw, string expectedIdentifier)
    {
        return Result
            .Try(() => JArray.Parse(metadataRaw))
            .Map(arr =>
            {
                foreach (var item in arr)
                {
                    if (item is JArray pair &&
                        pair.Count >= 2 &&
                        pair[0]?.Value<string>() == "text/identifier" &&
                        string.Equals(pair[1]?.Value<string>(), expectedIdentifier, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            })
            // If parsing fails or any unexpected error happens, we degrade to false as original behavior
            .OnFailureCompensate(_ => Result.Success(false));
    }
}