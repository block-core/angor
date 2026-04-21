using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App.Test.Integration.Helpers;

/// <summary>
/// Minimal ThunderHub GraphQL client for paying BOLT11 invoices in integration tests.
/// Authenticates via the get_session_token mutation, then pays invoices via the pay mutation.
/// </summary>
public class ThunderHubClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private string? _sessionToken;

    public ThunderHubClient(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _http = new HttpClient();
    }

    /// <summary>
    /// Log in to ThunderHub with the given account ID and password.
    /// Must be called before PayInvoiceAsync.
    /// </summary>
    public async Task LoginAsync(string accountId, string password)
    {
        var query = new
        {
            query = @"mutation GetSessionToken($id: String!, $password: String!) {
                getSessionToken(id: $id, password: $password)
            }",
            variables = new { id = accountId, password }
        };

        var response = await PostGraphqlAsync(query);
        var doc = JsonDocument.Parse(response);

        if (doc.RootElement.TryGetProperty("errors", out var errors))
            throw new Exception($"ThunderHub login failed: {errors}");

        _sessionToken = doc.RootElement
            .GetProperty("data")
            .GetProperty("getSessionToken")
            .GetString();

        if (string.IsNullOrEmpty(_sessionToken))
            throw new Exception("ThunderHub login returned empty session token");
    }

    /// <summary>
    /// Pay a BOLT11 invoice via the authenticated ThunderHub node.
    /// </summary>
    public async Task<bool> PayInvoiceAsync(string bolt11Invoice, float maxFeeSats = 1000, float maxPaths = 5)
    {
        if (_sessionToken == null)
            throw new InvalidOperationException("Must call LoginAsync before PayInvoiceAsync");

        var query = new
        {
            query = @"mutation Pay($request: String!, $max_fee: Float!, $max_paths: Float!) {
                pay(request: $request, max_fee: $max_fee, max_paths: $max_paths)
            }",
            variables = new { request = bolt11Invoice, max_fee = maxFeeSats, max_paths = maxPaths }
        };

        var response = await PostGraphqlAsync(query);
        var doc = JsonDocument.Parse(response);

        if (doc.RootElement.TryGetProperty("errors", out var errors))
            throw new Exception($"ThunderHub pay failed: {errors}");

        return doc.RootElement
            .GetProperty("data")
            .GetProperty("pay")
            .GetBoolean();
    }

    private async Task<string> PostGraphqlAsync(object body)
    {
        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/graphql")
        {
            Content = content
        };

        if (_sessionToken != null)
            request.Headers.Add("Cookie", $"SSOAuth={_sessionToken}");

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public void Dispose() => _http.Dispose();
}
