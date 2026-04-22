using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace App.Test.Integration.Helpers;

/// <summary>
/// Minimal ThunderHub GraphQL client for paying BOLT11 invoices in integration tests.
/// Authenticates via the getSessionToken mutation (extracts the Thub-Auth JWT from the
/// Set-Cookie header), then pays invoices via the pay mutation.
/// </summary>
public class ThunderHubClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private string? _authCookie;

    public ThunderHubClient(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        // Don't auto-follow redirects or handle cookies — we manage the auth cookie manually.
        _http = new HttpClient(new HttpClientHandler { UseCookies = false });
    }

    /// <summary>
    /// Log in to ThunderHub with the given account name and password.
    /// Queries getServerAccounts to resolve the account hash ID, then authenticates.
    /// </summary>
    public async Task LoginAsync(string accountName, string password)
    {
        // Step 1: Resolve the account hash ID from the display name
        var accountId = await ResolveAccountIdAsync(accountName);

        // Step 2: Call getSessionToken — the real auth token comes back as a Set-Cookie header
        var query = new
        {
            query = @"mutation GetSessionToken($id: String!, $password: String!) {
                getSessionToken(id: $id, password: $password)
            }",
            variables = new { id = accountId, password }
        };

        var (responseBody, responseMessage) = await PostGraphqlRawAsync(query);
        var doc = JsonDocument.Parse(responseBody);

        if (doc.RootElement.TryGetProperty("errors", out var errors))
            throw new Exception($"ThunderHub login failed: {errors}");

        // Extract the Thub-Auth JWT from the Set-Cookie response header
        _authCookie = ExtractAuthCookie(responseMessage);
        if (string.IsNullOrEmpty(_authCookie))
            throw new Exception("ThunderHub login succeeded but no Thub-Auth cookie was returned");
    }

    /// <summary>
    /// Pay a BOLT11 invoice via the authenticated ThunderHub node.
    /// </summary>
    public async Task<bool> PayInvoiceAsync(string bolt11Invoice, float maxFeeSats = 1000, float maxPaths = 5)
    {
        if (_authCookie == null)
            throw new InvalidOperationException("Must call LoginAsync before PayInvoiceAsync");

        var query = new
        {
            query = @"mutation Pay($request: String!, $max_fee: Float!, $max_paths: Float!) {
                pay(request: $request, max_fee: $max_fee, max_paths: $max_paths)
            }",
            variables = new { request = bolt11Invoice, max_fee = maxFeeSats, max_paths = maxPaths }
        };

        var (responseBody, _) = await PostGraphqlRawAsync(query);
        var doc = JsonDocument.Parse(responseBody);

        if (doc.RootElement.TryGetProperty("errors", out var errors))
            throw new Exception($"ThunderHub pay failed: {errors}");

        return doc.RootElement
            .GetProperty("data")
            .GetProperty("pay")
            .GetBoolean();
    }

    private async Task<string> ResolveAccountIdAsync(string accountName)
    {
        var query = new { query = "{ getServerAccounts { name id } }" };
        var (responseBody, _) = await PostGraphqlRawAsync(query);
        var doc = JsonDocument.Parse(responseBody);

        if (doc.RootElement.TryGetProperty("errors", out var errors))
            throw new Exception($"ThunderHub getServerAccounts failed: {errors}");

        var accounts = doc.RootElement.GetProperty("data").GetProperty("getServerAccounts");
        foreach (var account in accounts.EnumerateArray())
        {
            var name = account.GetProperty("name").GetString();
            if (string.Equals(name, accountName, StringComparison.OrdinalIgnoreCase))
                return account.GetProperty("id").GetString()!;
        }

        // List available accounts in the error message
        var available = string.Join(", ", accounts.EnumerateArray()
            .Select(a => $"'{a.GetProperty("name").GetString()}'"));
        throw new Exception($"ThunderHub account '{accountName}' not found. Available: {available}");
    }

    private static string? ExtractAuthCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
            return null;

        foreach (var cookie in cookies)
        {
            // Format: "Thub-Auth=eyJ...JWT...; Path=/; HttpOnly; SameSite=Strict"
            if (cookie.StartsWith("Thub-Auth=", StringComparison.OrdinalIgnoreCase))
            {
                var value = cookie.Split(';')[0]; // "Thub-Auth=eyJ..."
                return value;                      // Full "Thub-Auth=..." for the Cookie header
            }
        }

        return null;
    }

    private async Task<(string Body, HttpResponseMessage Response)> PostGraphqlRawAsync(object body)
    {
        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/graphql")
        {
            Content = content
        };

        if (_authCookie != null)
            request.Headers.Add("Cookie", _authCookie);

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var responseBody = await response.Content.ReadAsStringAsync();
        return (responseBody, response);
    }

    public void Dispose() => _http.Dispose();
}
