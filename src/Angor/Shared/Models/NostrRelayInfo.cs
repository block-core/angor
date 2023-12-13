using System.Text.Json.Serialization;

namespace Angor.Shared.Models;

public class NostrRelayInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("description")]
    public string Description { get; set; }
    [JsonPropertyName("pubkey")]
    public string PubKey { get; set; }
    [JsonPropertyName("contact")]
    public string Contact { get; set; }
    [JsonPropertyName("supported_nips")]
    public int[] SupportedNips { get; set; }
    [JsonPropertyName("software")]
    public string Software { get; set; }
    [JsonPropertyName("version")]
    public string Version { get; set; }
}