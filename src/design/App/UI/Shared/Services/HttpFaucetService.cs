using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace App.UI.Shared.Services;

/// <summary>
/// Default <see cref="IFaucetService"/> that issues an HTTP GET against the
/// configured faucet endpoint.
/// </summary>
public sealed class HttpFaucetService : IFaucetService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FaucetOptions _options;
    private readonly ILogger<HttpFaucetService> _logger;

    public HttpFaucetService(
        IHttpClientFactory httpClientFactory,
        FaucetOptions options,
        ILogger<HttpFaucetService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<Result> RequestCoinsAsync(string address, decimal amountBtc, CancellationToken cancellationToken = default)
    {
        var amountString = amountBtc.ToString("0.########", CultureInfo.InvariantCulture);
        var path = string.Format(CultureInfo.InvariantCulture, _options.SendPathTemplate, address, amountString);
        var url = $"{_options.BaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";

        _logger.LogInformation("Calling faucet {Url}", url);

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync(url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return Result.Success();
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Faucet request failed: {StatusCode} {Reason} {Body}", response.StatusCode, response.ReasonPhrase, body);
            return Result.Failure($"Faucet request failed: {response.ReasonPhrase} - {body}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Faucet request threw an exception");
            return Result.Failure(ex.Message);
        }
    }
}
