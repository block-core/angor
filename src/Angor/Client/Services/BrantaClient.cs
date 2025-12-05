using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Angor.Client.Services.Branta.V2
{
    public class BrantaClient
    {
        private readonly HttpClient _httpClient;

        public BrantaClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<Payment>> GetPaymentsAsync(string address)
        {
            var response = await _httpClient.GetAsync($"/v2/payments/{address}");

            if (!response.IsSuccessStatusCode || response?.Content == null)
            {
                return [];
            }

            return await response.Content.ReadFromJsonAsync<List<Payment>>() ?? [];
        }
    }

    public class Destination
    {
        public required string Value { get; set; }

        [JsonPropertyName("primary")]
        public bool IsPrimary { get; set; }

        [JsonPropertyName("zk")]
        public bool IsZk { get; set; }
    }

    public class Payment
    {
        public string? Description { get; set; }

        public required List<Destination> Destinations { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedDate { get; set; }

        public int TTL { get; set; }

        public string? Metadata { get; set; }

        public required string Platform { get; set; }

        [JsonPropertyName("platform_logo_url")]
        public required string PlatformLogoUrl { get; set; }
    }
}
