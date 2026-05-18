using System.Text.Json.Serialization;

namespace App.Automation;

/// <summary>
/// DTOs for high-level test flow endpoints.
/// These encapsulate multi-step UI flows that would require dozens of individual HTTP calls.
/// </summary>
public static class AutomationFlowDtos
{
    public sealed class CreateWalletAndFundRequest
    {
        [JsonPropertyName("profileName")]
        public string ProfileName { get; init; } = "";
    }

    public sealed class CreateWalletAndFundResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("walletId")]
        public string? WalletId { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }

    public sealed class CreateFundProjectRequest
    {
        [JsonPropertyName("projectName")]
        public string ProjectName { get; init; } = "";

        [JsonPropertyName("projectAbout")]
        public string ProjectAbout { get; init; } = "";

        [JsonPropertyName("bannerUrl")]
        public string BannerUrl { get; init; } = "";

        [JsonPropertyName("profileUrl")]
        public string ProfileUrl { get; init; } = "";

        [JsonPropertyName("thresholdAmountBtc")]
        public string ThresholdAmountBtc { get; init; } = "0.01";

        [JsonPropertyName("payoutDay")]
        public string PayoutDay { get; init; } = "";

        [JsonPropertyName("runId")]
        public string RunId { get; init; } = "";
    }

    public sealed class CreateInvestProjectRequest
    {
        [JsonPropertyName("projectName")]
        public string ProjectName { get; init; } = "";

        [JsonPropertyName("projectAbout")]
        public string ProjectAbout { get; init; } = "";

        [JsonPropertyName("bannerUrl")]
        public string BannerUrl { get; init; } = "";

        [JsonPropertyName("profileUrl")]
        public string ProfileUrl { get; init; } = "";

        [JsonPropertyName("runId")]
        public string RunId { get; init; } = "";
    }

    public sealed class ProjectCreatedResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("projectIdentifier")]
        public string? ProjectIdentifier { get; init; }

        [JsonPropertyName("ownerWalletId")]
        public string? OwnerWalletId { get; init; }

        [JsonPropertyName("projectType")]
        public string? ProjectType { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }

    public sealed class InvestInProjectRequest
    {
        [JsonPropertyName("projectIdentifier")]
        public string ProjectIdentifier { get; init; } = "";

        [JsonPropertyName("runId")]
        public string RunId { get; init; } = "";

        [JsonPropertyName("projectName")]
        public string ProjectName { get; init; } = "";

        [JsonPropertyName("amountBtc")]
        public string AmountBtc { get; init; } = "";

        [JsonPropertyName("expectFounderApproval")]
        public bool ExpectFounderApproval { get; init; }

        [JsonPropertyName("targetPatternStageCount")]
        public int TargetPatternStageCount { get; init; }
    }

    public sealed class InvestResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("isAutoApproved")]
        public bool IsAutoApproved { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }

    public sealed class ApproveInvestmentsRequest
    {
        [JsonPropertyName("projectIdentifier")]
        public string ProjectIdentifier { get; init; } = "";

        [JsonPropertyName("expectedCount")]
        public int ExpectedCount { get; init; }

        [JsonPropertyName("batch")]
        public bool Batch { get; init; } = true;
    }

    public sealed class ApproveInvestmentsResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("approvedCount")]
        public int ApprovedCount { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }

    public sealed class ConfirmInvestmentRequest
    {
        [JsonPropertyName("projectIdentifier")]
        public string ProjectIdentifier { get; init; } = "";
    }

    public sealed class ConfirmInvestmentResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("step")]
        public int Step { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }

    public sealed class ClaimStageRequest
    {
        [JsonPropertyName("projectIdentifier")]
        public string ProjectIdentifier { get; init; } = "";

        [JsonPropertyName("stageNumber")]
        public int StageNumber { get; init; } = 1;

        [JsonPropertyName("expectedUtxoCount")]
        public int ExpectedUtxoCount { get; init; }
    }

    public sealed class RecoveryRequest
    {
        [JsonPropertyName("projectIdentifier")]
        public string ProjectIdentifier { get; init; } = "";

        [JsonPropertyName("action")]
        public string Action { get; init; } = "";
    }

    public sealed class RecoveryResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("actionKey")]
        public string? ActionKey { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }

    public sealed class ReleaseFundsRequest
    {
        [JsonPropertyName("projectIdentifier")]
        public string ProjectIdentifier { get; init; } = "";
    }
}
