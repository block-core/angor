using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Angor.Shared.Services;

/// <summary>Recovers idempotent indexer reads without changing the user's preferred server.</summary>
public sealed class IndexerFailoverHandler(
    INetworkStorage storage,
    INetworkConfiguration configuration,
    ILogger<IndexerFailoverHandler> logger) : DelegatingHandler
{
    public const string ClientName = "AngorIndexer";
    private readonly ConcurrentDictionary<string, Route> routes = new();
    internal TimeSpan AttemptTimeout { get; init; } = TimeSpan.FromSeconds(8);

    private sealed class Route
    {
        public readonly SemaphoreSlim Recovery = new(1, 1);
        public readonly ConcurrentDictionary<string, DateTimeOffset> FailedUntil = new();
        public string? Preferred;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Never replay a transaction broadcast or other write.
        if (request.Method != HttpMethod.Get || request.RequestUri == null)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        string genesis = configuration.GetGenesisBlockHash();
        string origin = request.RequestUri.GetLeftPart(UriPartial.Authority);
        string[] servers = storage.GetSettings().Indexers.Select(x => x.Url.TrimEnd('/'))
            .Where(x => Uri.TryCreate(x, UriKind.Absolute, out Uri? uri)
                && (uri.Scheme == "https" || uri.Scheme == "http"))
            .Prepend(origin).Distinct().ToArray();
        Route route = routes.GetOrAdd(genesis + "|" + origin + "|" + string.Join("|", servers), _ => new Route());
        string first = route.Preferred ?? origin;
        HttpResponseMessage? response = null;
        if (!CoolingDown(route, first))
        {
            response = await TryReadAsync(request, first, genesis, cancellationToken);
            if (response != null)
            {
                return response;
            }
            route.FailedUntil[first] = DateTimeOffset.UtcNow.AddSeconds(30);
        }

        // Only one caller probes replacements; parallel wallet/project reads reuse its result.
        await route.Recovery.WaitAsync(cancellationToken);
        try
        {
            foreach (string server in servers.OrderBy(x => x == route.Preferred ? 0 : 1))
            {
                if (CoolingDown(route, server))
                {
                    continue;
                }

                if (server != origin && server != route.Preferred
                    && !await MatchesNetworkAsync(server, genesis, cancellationToken))
                {
                    route.FailedUntil[server] = DateTimeOffset.UtcNow.AddSeconds(30);
                    continue;
                }

                response = await TryReadAsync(request, server, genesis, cancellationToken);
                if (response != null)
                {
                    route.Preferred = server;
                    logger.LogInformation("Recovered indexer reads using {Server}", server);
                    return response;
                }
                route.FailedUntil[server] = DateTimeOffset.UtcNow.AddSeconds(30);
            }
        }
        finally
        {
            route.Recovery.Release();
        }

        throw new HttpRequestException("Unable to reach a working server for the selected network. Please try again shortly.");
    }

    private static bool CoolingDown(Route route, string server)
    {
        return route.FailedUntil.TryGetValue(server, out DateTimeOffset until) && until > DateTimeOffset.UtcNow;
    }

    private async Task<bool> MatchesNetworkAsync(string server, string genesis, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(AttemptTimeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, server + "/api/v1/block-height/0");
            using HttpResponseMessage response = await base.SendAsync(request, timeout.Token);
            string hash = (await response.Content.ReadAsStringAsync(timeout.Token)).Trim();
            return response.IsSuccessStatusCode && hash.Length == 64 && hash.All(Uri.IsHexDigit)
                && !string.IsNullOrWhiteSpace(genesis) && hash.StartsWith(genesis, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private async Task<HttpResponseMessage?> TryReadAsync(
        HttpRequestMessage original, string server, string genesis, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (configuration.GetGenesisBlockHash() != genesis)
        {
            throw new HttpRequestException("The selected network changed during the request. Please try again.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(AttemptTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, server + original.RequestUri!.PathAndQuery);
        foreach (var header in original.Headers)
        {
            // Authentication for a custom primary must not be forwarded to a different server.
            if (server != original.RequestUri.GetLeftPart(UriPartial.Authority)
                && !new[] { "Accept", "Accept-Language", "User-Agent" }
                    .Contains(header.Key, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        HttpResponseMessage? response = null;
        try
        {
            response = await base.SendAsync(request, timeout.Token);
            int status = (int)response.StatusCode;
            if (status >= 500 || status is 403 or 408 or 429)
            {
                response.Dispose();
                return null;
            }

            // Proxies can return an HTML/plain-text error page with HTTP 200.
            string path = original.RequestUri.AbsolutePath;
            bool expectsJson = path.Contains("/address/") || path.Contains("/query/Angor/")
                || path.EndsWith("/fees/recommended") || path.Contains("/tx/") && !path.EndsWith("/hex");
            if (response.IsSuccessStatusCode && expectsJson)
            {
                using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
                if (document.RootElement.ValueKind is not (JsonValueKind.Array or JsonValueKind.Object))
                {
                    throw new JsonException("Expected an indexer JSON response.");
                }
            }
            return response;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            response?.Dispose();
            logger.LogWarning("Indexer read failed at {Server}: {Reason}", server, ex.Message);
            return null;
        }
    }
}
