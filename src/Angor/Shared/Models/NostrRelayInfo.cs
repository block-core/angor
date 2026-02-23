using System.Text.Json.Serialization;

namespace Angor.Shared.Models;

public class NostrRelayInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    [JsonPropertyName("pubkey")]
    public string PubKey { get; set; } = string.Empty;
    [JsonPropertyName("contact")]
    public string Contact { get; set; } = string.Empty;
    [JsonPropertyName("supported_nips")]
    public int[] SupportedNips { get; set; } = [];
    [JsonPropertyName("software")]
    public string Software { get; set; } = string.Empty;
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
}