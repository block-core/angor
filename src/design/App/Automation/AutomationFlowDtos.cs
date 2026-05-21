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

        [JsonPropertyName("seedWords")]
        public string? SeedWords { get; init; }

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

        [JsonPropertyName("targetAmountBtc")]
        public string TargetAmountBtc { get; init; } = "1.0";

        [JsonPropertyName("thresholdAmountBtc")]
        public string ThresholdAmountBtc { get; init; } = "0.01";

        [JsonPropertyName("penaltyDays")]
        public int PenaltyDays { get; init; }

        /// <summary>
        /// "Weekly" or "Monthly". Defaults to "Weekly".
        /// </summary>
        [JsonPropertyName("payoutFrequency")]
        public string PayoutFrequency { get; init; } = "Weekly";

        /// <summary>
        /// Number of installments: 3, 6, or 9. Defaults to 3.
        /// </summary>
        [JsonPropertyName("installmentCount")]
        public int InstallmentCount { get; init; } = 3;

        /// <summary>
        /// Day name for weekly payouts (e.g. "Monday"). Required when PayoutFrequency is "Weekly".
        /// </summary>
        [JsonPropertyName("payoutDay")]
        public string PayoutDay { get; init; } = "";

        /// <summary>
        /// Day of month (1-29) for monthly payouts. Required when PayoutFrequency is "Monthly".
        /// </summary>
        [JsonPropertyName("monthlyPayoutDay")]
        public int MonthlyPayoutDay { get; init; }

        /// <summary>
        /// Override the project start date (yyyy-MM-dd). When set, stage timelocks are calculated
        /// from this date instead of today. Use a past date to make stages immediately claimable
        /// in tests. Only effective in debug mode.
        /// </summary>
        [JsonPropertyName("startDate")]
        public string StartDate { get; init; } = "";

        [JsonPropertyName("runId")]
        public string RunId { get; init; } = "";
    }

    public sealed class CancelInvestmentRequest
    {
        [JsonPropertyName("projectIdentifier")]
        public string ProjectIdentifier { get; init; } = "";

        /// <summary>
        /// Which cancel stage: "beforeApproval" (Step 1) or "afterApproval" (Step 2).
        /// </summary>
        [JsonPropertyName("cancelStage")]
        public string CancelStage { get; init; } = "beforeApproval";
    }

    public sealed class CancelInvestmentResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }
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

    public sealed class EditProjectProfileRequest
    {
        [JsonPropertyName("projectIdentifier")]
        public string ProjectIdentifier { get; init; } = "";

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; init; }

        [JsonPropertyName("about")]
        public string? About { get; init; }

        [JsonPropertyName("picture")]
        public string? Picture { get; init; }

        [JsonPropertyName("banner")]
        public string? Banner { get; init; }

        [JsonPropertyName("website")]
        public string? Website { get; init; }

        [JsonPropertyName("projectContent")]
        public string? ProjectContent { get; init; }
    }

    public sealed class EditProjectProfileResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }

    public sealed class FetchProjectProfileRequest
    {
        [JsonPropertyName("projectIdentifier")]
        public string ProjectIdentifier { get; init; } = "";
    }

    public sealed class FetchProjectProfileResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; init; }

        [JsonPropertyName("about")]
        public string? About { get; init; }

        [JsonPropertyName("picture")]
        public string? Picture { get; init; }

        [JsonPropertyName("banner")]
        public string? Banner { get; init; }

        [JsonPropertyName("website")]
        public string? Website { get; init; }

        [JsonPropertyName("projectContent")]
        public string? ProjectContent { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }

    public sealed class UploadToBlossomRequest
    {
        [JsonPropertyName("projectIdentifier")]
        public string ProjectIdentifier { get; init; } = "";

        [JsonPropertyName("imageUrl")]
        public string ImageUrl { get; init; } = "";

        [JsonPropertyName("blossomServer")]
        public string BlossomServer { get; init; } = "https://blossom.angor.io";
    }

    public sealed class UploadToBlossomResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("uploadedUrl")]
        public string? UploadedUrl { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }

    public sealed class ImportWalletRequest
    {
        [JsonPropertyName("seedWords")]
        public string SeedWords { get; init; } = "";

        [JsonPropertyName("profileName")]
        public string ProfileName { get; init; } = "";
    }

    public sealed class ImportWalletResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("walletId")]
        public string? WalletId { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }
}
