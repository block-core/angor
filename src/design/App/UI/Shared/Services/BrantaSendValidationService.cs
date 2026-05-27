using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace App.UI.Shared.Services;

public partial class BrantaSendValidationService : IBrantaSendValidationService
{
    private const string BrantaBaseUrl = "https://guardrail.branta.pro";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BrantaSendValidationService> _logger;

    public BrantaSendValidationService(
        IHttpClientFactory httpClientFactory,
        ILogger<BrantaSendValidationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<BrantaSendValidationResult> ValidateAsync(string destination, CancellationToken ct = default)
    {
        var lookupValue = ExtractLookupValue(destination);
        if (string.IsNullOrWhiteSpace(lookupValue))
        {
            return new BrantaSendValidationResult(false, "Address failed Branta validation");
        }

        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(BrantaBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);

            using var response = await client.GetAsync($"/v2/payments/{Uri.EscapeDataString(lookupValue)}", ct);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength == 0)
            {
                return new BrantaSendValidationResult(false, "Address failed Branta validation");
            }

            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: ct);
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
            {
                return new BrantaSendValidationResult(false, "Address failed Branta validation");
            }

            return new BrantaSendValidationResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Branta validation failed for destination");
            return new BrantaSendValidationResult(false, "Unable to validate address with Branta");
        }
    }

    private static string ExtractLookupValue(string destination)
    {
        var trimmed = destination.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return "";
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return trimmed;
        }

        if (uri.Scheme is not ("bitcoin" or "lightning"))
        {
            return trimmed;
        }

        var query = ParseQueryString(uri.Query);
        if (query.TryGetValue("branta_id", out var brantaId) && !string.IsNullOrWhiteSpace(brantaId))
        {
            return brantaId;
        }

        var match = DestinationRegex().Match(trimmed);
        return match.Success ? match.Value : "";
    }

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var value = parts.Length > 1
                ? Uri.UnescapeDataString(parts[1])
                : "";
            result[key] = value;
        }

        return result;
    }

    [GeneratedRegex(@"(?<=:)[^?]*", RegexOptions.CultureInvariant)]
    private static partial Regex DestinationRegex();
}
