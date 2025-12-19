using System.Net.Http;

namespace Angor.Sdk.Tests.Funding.TestDoubles;

/// <summary>
/// Test implementation of IHttpClientFactory for integration tests.
/// Creates HttpClient instances with a configured base address.
/// </summary>
public class TestHttpClientFactory : IHttpClientFactory
{
    private readonly string _baseAddress;

    public TestHttpClientFactory(string baseAddress)
    {
        _baseAddress = baseAddress;
    }

    public HttpClient CreateClient(string name)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(_baseAddress),
            Timeout = TimeSpan.FromSeconds(30)
        };
        return client;
    }
}

