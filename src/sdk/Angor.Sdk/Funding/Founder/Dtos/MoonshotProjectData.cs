using System.Text.Json.Serialization;

namespace Angor.Sdk.Funding.Founder.Dtos;

public class MoonshotProjectData
{
    [JsonPropertyName("moonshot")]
    public MoonshotInfo Moonshot { get; set; } = new();

    [JsonPropertyName("projectType")]
    public string ProjectType { get; set; } = string.Empty;

    [JsonPropertyName("selectedBuilderPubkey")]
    public string SelectedBuilderPubkey { get; set; } = string.Empty;

    [JsonPropertyName("penaltyThreshold")]
    public string PenaltyThreshold { get; set; } = string.Empty;

    [JsonPropertyName("fundingPattern")]
    public FundingPatternInfo? FundingPattern { get; set; }
}

public class MoonshotInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("eventId")]
    public string EventId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("budget")]
    public string Budget { get; set; } = string.Empty;

    [JsonPropertyName("timeline")]
    public string Timeline { get; set; } = string.Empty;

    [JsonPropertyName("topics")]
    public List<string> Topics { get; set; } = new();

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("creatorPubkey")]
    public string CreatorPubkey { get; set; } = string.Empty;

    [JsonPropertyName("isExplorable")]
    public bool IsExplorable { get; set; }

    [JsonPropertyName("createdAt")]
    public long CreatedAt { get; set; }
}

public class FundingPatternInfo
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("duration")]
    public int Duration { get; set; }

    [JsonPropertyName("releaseDay")]
    public int ReleaseDay { get; set; }
}

