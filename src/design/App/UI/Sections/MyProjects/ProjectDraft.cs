namespace App.UI.Sections.MyProjects;

/// <summary>
/// Serializable snapshot of the Create Project wizard state.
/// Persisted to LiteDB so the user can resume after closing the app.
/// </summary>
public class ProjectDraft
{
    /// <summary>Keyed by wallet ID so each wallet gets its own draft.</summary>
    public string WalletId { get; set; } = "";

    // ── Navigation ──
    public int CurrentStep { get; set; } = 1;
    public int MaxStepReached { get; set; } = 1;
    public bool ShowWelcome { get; set; } = true;
    public bool ShowStep5Welcome { get; set; } = true;

    // ── Step 1: Project Type ──
    public string ProjectType { get; set; } = "";

    // ── Step 2: Project Profile ──
    public string ProjectName { get; set; } = "";
    public string ProjectAbout { get; set; } = "";
    public string ProjectWebsite { get; set; } = "";

    // ── Step 3: Project Images ──
    public string BannerUrl { get; set; } = "";
    public string ProfileUrl { get; set; } = "";

    // ── Step 4: Funding Config ──
    public string TargetAmount { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public int PenaltyDays { get; set; } = 90;
    public string ApprovalThreshold { get; set; } = "0.001";
    public string SubscriptionPrice { get; set; } = "";
    public DateTime? InvestStartDate { get; set; }
    public DateTime? InvestEndDate { get; set; }

    // ── Step 5: Stages/Payouts ──
    public string DurationValue { get; set; } = "0";
    public string DurationUnit { get; set; } = "Months";
    public int? DurationPreset { get; set; }
    public string ReleaseFrequency { get; set; } = "";
    public string PayoutFrequency { get; set; } = "";
    public int? MonthlyPayoutDate { get; set; }
    public string WeeklyPayoutDay { get; set; } = "";
    public List<int> SelectedInstallmentCounts { get; set; } = new();
    public bool IsAdvancedEditor { get; set; }
    public bool ShowGenerateForm { get; set; } = true;
    public string SelectedPayoutPattern { get; set; } = "";
    public string PayoutDay { get; set; } = "1";

    // ── Generated Stages ──
    public List<StageDraft> Stages { get; set; } = new();

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Serializable snapshot of a single stage in the wizard.
/// </summary>
public class StageDraft
{
    public int StageNumber { get; set; }
    public double PercentageValue { get; set; }
    public DateOnly ReleaseDateValue { get; set; }
    public string StageLabel { get; set; } = "Stage";
}
