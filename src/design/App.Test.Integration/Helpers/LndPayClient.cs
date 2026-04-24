using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace App.Test.Integration.Helpers;

/// <summary>
/// Minimal LND REST client for paying BOLT11 invoices in integration tests.
/// Uses the /v2/router/send streaming endpoint (SendPaymentV2) via our
/// self-hosted reverse proxy at thunderhub.thedude.cloud/lnd1-pay.
///
/// Auth: hex-encoded macaroon passed via the Grpc-Metadata-macaroon header.
/// The macaroon is scoped to offchain:read + offchain:write only.
/// </summary>
public class LndPayClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _macaroonHex;

    public LndPayClient(string baseUrl, string macaroonHex)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _macaroonHex = macaroonHex;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
    }

    /// <summary>
    /// Pay a BOLT11 invoice via the LND REST API (SendPaymentV2 streaming endpoint).
    /// Returns the payment preimage hex on success.
    /// </summary>
    public async Task<string> PayInvoiceAsync(string bolt11Invoice, int timeoutSeconds = 60, long feeLimitSat = 100)
    {
        var payload = new
        {
            payment_request = bolt11Invoice,
            timeout_seconds = timeoutSeconds,
            fee_limit_sat = feeLimitSat
        };

        var json = JsonSerializer.Serialize(payload);
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v2/router/send")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Grpc-Metadata-macaroon", _macaroonHex);

        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        // /v2/router/send is a streaming RPC: LND sends newline-delimited JSON objects,
        // one per payment state transition (IN_FLIGHT → SUCCEEDED or FAILED).
        // Read until we get a terminal state.
        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        string? lastLine = null;
        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            lastLine = line;

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            // Check for error wrapper
            if (root.TryGetProperty("error", out var error))
            {
                var message = error.TryGetProperty("message", out var msg) ? msg.GetString() : line;
                throw new Exception($"LND payment error: {message}");
            }

            // The payment state lives in result.status
            if (!root.TryGetProperty("result", out var result))
                continue;

            if (!result.TryGetProperty("status", out var statusProp))
                continue;

            var status = statusProp.GetString();

            switch (status)
            {
                case "SUCCEEDED":
                    var preimage = result.TryGetProperty("payment_preimage", out var pi)
                        ? pi.GetString() ?? ""
                        : "";
                    return preimage;

                case "FAILED":
                    var reason = result.TryGetProperty("failure_reason", out var fr)
                        ? fr.GetString()
                        : "unknown";
                    throw new Exception($"LND payment failed: {reason}");

                // IN_FLIGHT and other transitional states — keep reading
            }
        }

        throw new Exception($"LND payment stream ended without terminal state. Last line: {lastLine ?? "(empty)"}");
    }

    public void Dispose() => _http.Dispose();
}
