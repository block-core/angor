using System.Collections.ObjectModel;
using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Utilities;
using App.UI.Sections.MyProjects.Deploy;
using App.UI.Shared;
using App.UI.Shared.Services;
using ReactiveUI;
using SdkProjectType = Angor.Shared.Models.ProjectType;

namespace App.UI.Sections.MyProjects;

/// <summary>
/// A duration preset item with a numeric value and display label.
/// E.g. DurationPresetItem(3, "3 Months")
/// </summary>
public record DurationPresetItem(int Value, string Label);

/// <summary>
/// A stage in the project's release/payout schedule.
/// Generated from the payout pattern selection.
/// </summary>
public class ProjectStageViewModel
{
    public int StageNumber { get; set; }
    public string Percentage { get; set; } = "0%";
    public string ReleaseDate { get; set; } = "";
    public string AmountBtc { get; set; } = "0.00000000";

    /// <summary>Label for this stage row — "Stage", "Monthly Payout", "Weekly Payout", "Payment", etc.</summary>
    public string StageLabel { get; set; } = "Stage";

    /// <summary>
    /// Pre-formatted display text for the right side of the stage row.
    /// Investment: "8% (0.0800 BTC) released on 28th April 2026"
    /// Fund/Sub: "33% paid on 28th April 2026"
    /// </summary>
    public string DisplayText { get; set; } = "";
}

/// <summary>
/// ViewModel for the Create/Launch Project wizard.
/// Visual-layer only — all fields are sample/stub data with no SDK dependencies.
///
/// Vue wizard structure (6 steps for all 3 project types):
///   1. Project Type — choose invest / fund / subscription
///   2. Project Profile — name, about, website
///   3. Project Images — banner URL, profile/logo URL
///   4. Funding Configuration / Goal / Subscription Price
///   5. Stages / Payouts
///   6. Review & Deploy
///
/// Vue layout: two-column — vertical stepper on left, content on right.
/// Welcome screen shown on step 1 before type selection (Xe.value=true).
/// </summary>
public partial class CreateProjectViewModel : ReactiveObject
{
    // ── Wizard navigation state ──
    [Reactive] private int currentStep = 1;
    [Reactive] private int maxStepReached = 1;
    [Reactive] private bool showWelcome = true; // Vue: Xe.value — welcome card before type selection
    [Reactive] private bool showStep5Welcome = true; // Interstitial "welcome" screen before stages/payouts form

    // ── Step 1: Project Type ──
    [Reactive] private string projectType = ""; // "investment", "fund", "subscription"

    // ── Step 2: Project Profile ──
    [Reactive] private string projectName = "";
    [Reactive] private string projectAbout = "";
    [Reactive] private string projectWebsite = "";

    // ── Validation errors (Vue: formError, nameError, aboutError, etc.) ──
    [Reactive] private string formError = "";
    [Reactive] private string nameError = "";
    [Reactive] private string aboutError = "";
    [Reactive] private string targetAmountError = "";
    [Reactive] private string endDateError = "";
    [Reactive] private string stagesError = "";

    // ── Step 3: Project Images ──
    [Reactive] private string bannerUrl = "";
    [Reactive] private string profileUrl = "";

    // ── Step 4: Funding Config (varies by type) ──
    [Reactive] private string targetAmount = "";
    [Reactive] private string startDate = "";
    [Reactive] private string endDate = "";
    [Reactive] private int penaltyDays = 90;
    [Reactive] private string approvalThreshold = "0.001";
    [Reactive] private string subscriptionPrice = ""; // subscription type only

    // CalendarDatePicker dates (Investment type)
    [Reactive] private DateTime? investStartDate;
    [Reactive] private DateTime? investEndDate;

    // ── Step 5: Stages/Payouts ──
    // Investment type: duration-based
    [Reactive] private string durationValue = "0"; // numeric input
    [Reactive] private string durationUnit = "Months"; // Months, Weeks, Days
    [Reactive] private int? durationPreset; // 3, 6, 12, 18, 24
    [Reactive] private string releaseFrequency = ""; // Weekly, Monthly, Bi-Monthly, Quarterly

    // Fund/Subscription type: payout-based
    [Reactive] private string payoutFrequency = ""; // "Monthly" or "Weekly"
    [Reactive] private int? monthlyPayoutDate; // 1-29
    [Reactive] private string weeklyPayoutDay = ""; // Mon, Tue, ... Sun

    /// <summary>Multiselect installment counts (e.g. [3, 6, 9]). Vue: installmentCounts array.</summary>
    public ObservableCollection<int> SelectedInstallmentCounts { get; } = new();

    // Shared
    [Reactive] private bool isAdvancedEditor;
    [Reactive] private bool showGenerateForm = true; // controls visibility of generate form vs collapsed header

    // Kept for backward compat (old pattern approach — can be removed later)
    [Reactive] private string selectedPayoutPattern = ""; // pattern1..4
    [Reactive] private string payoutDay = "1";

    // ── Step 6: Deploy ──
    [Reactive] private bool isDeploying;
    [Reactive] private bool isDeployed;

    /// <summary>
    /// Callback to notify parent when project is deployed.
    /// Called with the wizard VM so the parent can extract project data,
    /// add it to the list, and close the wizard.
    /// In the Vue prototype, goToMyProjects() does all of this in one shot:
    /// creates project, adds to list, closes wizard, navigates to my-projects.
    /// </summary>
    public Action? OnProjectDeployed { get; set; }

    /// <summary>The deploy flow overlay ViewModel.</summary>
    public DeployFlowViewModel DeployFlow { get; }

    private readonly ICurrencyService _currencyService;
    private readonly INetworkConfiguration _networkConfiguration;

    /// <summary>
    /// True when debug mode is enabled AND on testnet.
    /// Controls visibility of the "Debug Prefill Data" button in Step 2.
    /// </summary>
    public bool IsDebugMode =>
        _networkConfiguration.GetDebugMode() &&
        _networkConfiguration.GetNetwork().Name != "Main";

    public string CurrencySymbol => _currencyService.Symbol;

    /// <summary>e.g. "Target Amount (BTC) *"</summary>
    public string TargetAmountLabel => _currencyService.TargetAmountLabel;

    /// <summary>e.g. "Goal (BTC) *"</summary>
    public string GoalLabel => _currencyService.GoalLabel;

    /// <summary>e.g. "Price per period (BTC) *"</summary>
    public string PricePerPeriodLabel => _currencyService.PricePerPeriodLabel;

    public CreateProjectViewModel(DeployFlowViewModel deployFlow, ICurrencyService currencyService, INetworkConfiguration networkConfiguration)
    {
        DeployFlow = deployFlow;
        _currencyService = currencyService;
        _networkConfiguration = networkConfiguration;
        // Default start date to today
        StartDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
        InvestStartDate = DateTime.Now;

        // Generate sample stages when a payout pattern is selected (legacy)
        this.WhenAnyValue(x => x.SelectedPayoutPattern)
            .Where(p => !string.IsNullOrEmpty(p))
            .Subscribe(_ => GenerateStages());

        // Sync CalendarDatePicker dates to string properties for review step
        this.WhenAnyValue(x => x.InvestStartDate)
            .Subscribe(d => StartDate = d?.ToString("yyyy-MM-dd") ?? "");
        this.WhenAnyValue(x => x.InvestEndDate)
            .Subscribe(d => EndDate = d?.ToString("yyyy-MM-dd") ?? "");

        // When duration preset changes, sync to duration value (don't force unit — presets already match current unit)
        this.WhenAnyValue(x => x.DurationPreset)
            .Where(d => d.HasValue)
            .Subscribe(d =>
            {
                DurationValue = d!.Value.ToString();
            });

        // When duration unit changes, notify presets list and clear preset selection
        this.WhenAnyValue(x => x.DurationUnit)
            .Subscribe(_ =>
            {
                DurationPreset = null;
                this.RaisePropertyChanged(nameof(DurationPresetItems));
            });

        // Track whether monthly or weekly payout date section is visible
        this.WhenAnyValue(x => x.PayoutFrequency)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(IsPayoutMonthly));
                this.RaisePropertyChanged(nameof(IsPayoutWeekly));
            });

        // ── Clear validation errors on user input (Vue: @input="formError = ''; nameError = ''") ──
        this.WhenAnyValue(x => x.ProjectName)
            .Subscribe(_ =>
            {
                FormError = "";
                NameError = "";
                this.RaisePropertyChanged(nameof(HasFormError));
                this.RaisePropertyChanged(nameof(HasNameError));
                this.RaisePropertyChanged(nameof(ProjectNameCharCount));
            });
        this.WhenAnyValue(x => x.ProjectAbout)
            .Subscribe(_ =>
            {
                FormError = "";
                AboutError = "";
                this.RaisePropertyChanged(nameof(HasFormError));
                this.RaisePropertyChanged(nameof(HasAboutError));
                this.RaisePropertyChanged(nameof(ProjectAboutCharCount));
            });
        this.WhenAnyValue(x => x.TargetAmount)
            .Subscribe(_ =>
            {
                FormError = "";
                TargetAmountError = "";
                this.RaisePropertyChanged(nameof(HasFormError));
                this.RaisePropertyChanged(nameof(HasTargetAmountError));
            });
        this.WhenAnyValue(x => x.SubscriptionPrice)
            .Subscribe(_ =>
            {
                FormError = "";
                TargetAmountError = "";
                this.RaisePropertyChanged(nameof(HasFormError));
                this.RaisePropertyChanged(nameof(HasTargetAmountError));
            });
        this.WhenAnyValue(x => x.InvestEndDate)
            .Subscribe(_ =>
            {
                FormError = "";
                EndDateError = "";
                this.RaisePropertyChanged(nameof(HasFormError));
                this.RaisePropertyChanged(nameof(HasEndDateError));
            });
    }

    public string DeployButtonText => IsDeploying ? "Deploying..." : "Deploy Project";

    // ── Step names — 6 steps shown in the vertical stepper ──
    public string[] StepNames =>
    [
        "Project Type", "Project Profile", "Project Images",
        Step4Title, Step5Title, "Review & Deploy"
    ];

    public int TotalSteps => 6;

    // ── Step visibility helpers (for XAML binding) ──
    public bool IsStep1 => CurrentStep == 1 && !ShowWelcome;
    public bool IsWelcome => CurrentStep == 1 && ShowWelcome;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;
    public bool IsStep4 => CurrentStep == 4;
    public bool IsStep5 => CurrentStep == 5;
    public bool IsStep5Welcome => CurrentStep == 5 && ShowStep5Welcome;
    public bool IsStep5Form => CurrentStep == 5 && !ShowStep5Welcome;
    public bool IsStep6 => CurrentStep == 6;

    // ── Navigation bar visibility — hide on welcome screens ──
    public bool ShowNavFooter => !ShowWelcome && !IsStep5Welcome;

    // ── Scroll content visibility — hide on any interstitial/welcome screen ──
    public bool ShowScrollContent => !ShowWelcome && !IsStep5Welcome;

    // ── Project type visibility ──
    public bool IsInvestment => ProjectType == "investment";
    public bool IsFund => ProjectType == "fund";
    public bool IsSubscription => ProjectType == "subscription";
    public bool IsTypeSelected => !string.IsNullOrEmpty(ProjectType);

    // ── Validation error visibility helpers (for XAML binding) ──
    public bool HasFormError => !string.IsNullOrEmpty(FormError);
    public bool HasNameError => !string.IsNullOrEmpty(NameError);
    public bool HasAboutError => !string.IsNullOrEmpty(AboutError);
    public bool HasTargetAmountError => !string.IsNullOrEmpty(TargetAmountError);
    public bool HasEndDateError => !string.IsNullOrEmpty(EndDateError);
    public bool HasStagesError => !string.IsNullOrEmpty(StagesError);

    // ── Char count displays (Vue: {n}/200, {n}/400) ──
    public string ProjectNameCharCount => $"{ProjectName?.Length ?? 0}/200";
    public string ProjectAboutCharCount => $"{ProjectAbout?.Length ?? 0}/400";

    // ── Cancel/Previous label for nav footer ──
    public string BackButtonText => CurrentStep == 1 ? "Cancel" : "Previous";

    // ── Type-specific terminology (via shared ProjectTypeTerminology) ──
    private Shared.ProjectType TypeEnum => ProjectTypeExtensions.FromLowerString(ProjectType);

    public string ActionVerb => ProjectTypeTerminology.ActionVerb(TypeEnum);

    public string AmountNoun => ProjectTypeTerminology.AmountNoun(TypeEnum);

    public string StageLabel => ProjectTypeTerminology.StageLabel(TypeEnum);

    public string ScheduleTitle => ProjectTypeTerminology.ScheduleTitle(TypeEnum);

    public string InvestorNoun => ProjectTypeTerminology.InvestorNounPlural(TypeEnum);

    public string TargetLabel => ProjectTypeTerminology.TargetLabelFull(TypeEnum);

    public string Step4Title => ProjectTypeTerminology.Step4Title(TypeEnum);

    public string Step5Title => ProjectTypeTerminology.Step5Title(TypeEnum);

    /// <summary>Step 5 interstitial welcome screen title.</summary>
    public string Step5WelcomeTitle => TypeEnum switch
    {
        Shared.ProjectType.Fund or Shared.ProjectType.Subscription => "Payouts",
        _ => "Set Funding Release Schedule"
    };

    /// <summary>Step 5 interstitial welcome screen subtitle (empty for investment type).</summary>
    public string Step5WelcomeSubtitle => TypeEnum switch
    {
        Shared.ProjectType.Fund => "Select a payout schedule for your recurring funding model.",
        Shared.ProjectType.Subscription => "On the next screen you'll choose what subscriptions you want to offer.",
        _ => ""
    };

    /// <summary>Step 5 interstitial welcome screen info box text.</summary>
    public string Step5WelcomeInfo => TypeEnum switch
    {
        Shared.ProjectType.Fund => "Payouts are scheduled based on your selection of weekly or monthly payouts and paid on the day you choose.",
        Shared.ProjectType.Subscription => "Subscribers pay their full plan upfront. You can offer 3, 6, and 12 month payment plans and choose your monthly payout day.",
        _ => "We protect investors and founders by releasing funds in stages as project milestones are completed, rather than receiving everything upfront."
    };

    // ── Navigation commands ──
    public bool CanGoNext => CurrentStep < TotalSteps && IsCurrentStepValid;
    public bool CanGoBack => CurrentStep > 1;
    public bool IsLastStep => CurrentStep == TotalSteps;

    public bool IsCurrentStepValid => CurrentStep switch
    {
        1 => !string.IsNullOrEmpty(ProjectType),
        2 => !string.IsNullOrWhiteSpace(ProjectName) && !string.IsNullOrWhiteSpace(ProjectAbout),
        3 => true, // images are optional
        4 => ProjectType switch
        {
            "subscription" => !string.IsNullOrWhiteSpace(SubscriptionPrice),
            _ => !string.IsNullOrWhiteSpace(TargetAmount)
        },
        5 => Stages.Count > 0,
        6 => true,
        _ => false
    };

    /// <summary>Dismiss welcome screen, show type selection.</summary>
    public void DismissWelcome()
    {
        ShowWelcome = false;
        RaiseAllStepProperties();
    }

    /// <summary>Dismiss step 5 interstitial, show stages/payouts form.</summary>
    public void DismissStep5Welcome()
    {
        ShowStep5Welcome = false;
        RaiseAllStepProperties();
    }

    /// <summary>
    /// Creates a ProjectValidator with the current debug mode state.
    /// Debug mode is only active when explicitly enabled AND on testnet.
    /// </summary>
    private ProjectValidator CreateValidator()
    {
        return new ProjectValidator(IsDebugMode);
    }

    public void GoNext()
    {
        if (CurrentStep >= TotalSteps) return;

        // ── Validate current step (Vue: nextStep() lines 9451-9629) ──
        ClearErrors();

        var validator = CreateValidator();

        switch (CurrentStep)
        {
            case 1:
                if (string.IsNullOrEmpty(ProjectType))
                {
                    FormError = "Please select a project type before proceeding";
                    RaiseErrorProperties();
                    return;
                }
                break;

            case 2:
            {
                var nameResult = validator.ValidateName(ProjectName);
                if (!nameResult.IsValid)
                {
                    FormError = nameResult.ErrorMessage!;
                    NameError = nameResult.ErrorMessage!;
                    RaiseErrorProperties();
                    return;
                }

                var descResult = validator.ValidateDescription(ProjectAbout);
                if (!descResult.IsValid)
                {
                    FormError = descResult.ErrorMessage!;
                    AboutError = descResult.ErrorMessage!;
                    RaiseErrorProperties();
                    return;
                }
                break;
            }

            case 3:
                // Images are optional — no validation
                break;

            case 4:
                if (ProjectType == "subscription")
                {
                    if (string.IsNullOrWhiteSpace(SubscriptionPrice) ||
                        !double.TryParse(SubscriptionPrice, out var subPrice) || subPrice <= 0)
                    {
                        FormError = "Please enter a valid subscription price";
                        TargetAmountError = "Subscription price is required";
                        RaiseErrorProperties();
                        return;
                    }
                }
                else if (ProjectType == "fund")
                {
                    if (string.IsNullOrWhiteSpace(TargetAmount) ||
                        !double.TryParse(TargetAmount, out var goalAmt) || goalAmt <= 0)
                    {
                        FormError = "Please enter a valid goal amount";
                        TargetAmountError = "Goal amount is required";
                        RaiseErrorProperties();
                        return;
                    }
                }
                else // investment
                {
                    if (string.IsNullOrWhiteSpace(TargetAmount) ||
                        !double.TryParse(TargetAmount, out var targetAmt) || targetAmt <= 0)
                    {
                        FormError = "Please enter a valid target amount";
                        TargetAmountError = "Target amount is required";
                        RaiseErrorProperties();
                        return;
                    }

                    // Production validation: target amount limits
                    var amountResult = validator.ValidateTargetAmount((decimal)double.Parse(TargetAmount,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture));
                    if (!amountResult.IsValid)
                    {
                        FormError = amountResult.ErrorMessage!;
                        TargetAmountError = amountResult.ErrorMessage!;
                        RaiseErrorProperties();
                        return;
                    }

                    if (!InvestEndDate.HasValue)
                    {
                        FormError = "Please enter the funding window end date";
                        EndDateError = "Please enter the funding window end date";
                        RaiseErrorProperties();
                        return;
                    }

                    // Production validation: funding end date
                    var endDateResult = validator.ValidateFundingEndDate(InvestEndDate.Value);
                    if (!endDateResult.IsValid)
                    {
                        FormError = endDateResult.ErrorMessage!;
                        EndDateError = endDateResult.ErrorMessage!;
                        RaiseErrorProperties();
                        return;
                    }

                    // Production validation: penalty days
                    var penaltyResult = validator.ValidatePenaltyDays(PenaltyDays);
                    if (!penaltyResult.IsValid)
                    {
                        FormError = penaltyResult.ErrorMessage!;
                        RaiseErrorProperties();
                        return;
                    }
                }
                break;

            case 5:
                if (Stages.Count == 0)
                {
                    FormError = IsInvestment
                        ? "Please create at least one funding stage before proceeding"
                        : "Please generate a payout schedule before proceeding";
                    StagesError = FormError;
                    RaiseErrorProperties();
                    return;
                }
                break;
        }

        CurrentStep++;
        if (CurrentStep > MaxStepReached)
            MaxStepReached = CurrentStep;
        RaiseAllStepProperties();
    }

    public void GoBack()
    {
        if (!CanGoBack) return;
        CurrentStep--;
        RaiseAllStepProperties();
    }

    public void GoToStep(int step)
    {
        if (step < 1 || step > MaxStepReached) return;
        CurrentStep = step;
        RaiseAllStepProperties();
    }

    public void SelectProjectType(string type)
    {
        ProjectType = type;
        ClearErrors();
        this.RaisePropertyChanged(nameof(IsInvestment));
        this.RaisePropertyChanged(nameof(IsFund));
        this.RaisePropertyChanged(nameof(IsSubscription));
        this.RaisePropertyChanged(nameof(IsTypeSelected));
        this.RaisePropertyChanged(nameof(StepNames));
        this.RaisePropertyChanged(nameof(Step4Title));
        this.RaisePropertyChanged(nameof(Step5Title));
        this.RaisePropertyChanged(nameof(Step5WelcomeTitle));
        this.RaisePropertyChanged(nameof(Step5WelcomeSubtitle));
        this.RaisePropertyChanged(nameof(Step5WelcomeInfo));
        this.RaisePropertyChanged(nameof(TargetLabel));
        this.RaisePropertyChanged(nameof(ActionVerb));
        this.RaisePropertyChanged(nameof(AmountNoun));
        this.RaisePropertyChanged(nameof(StageLabel));
        this.RaisePropertyChanged(nameof(ScheduleTitle));
        this.RaisePropertyChanged(nameof(InvestorNoun));
        this.RaisePropertyChanged(nameof(CanGoNext));
    }

    public void SelectPayoutPattern(string pattern)
    {
        SelectedPayoutPattern = pattern;
        this.RaisePropertyChanged(nameof(CanGoNext));
    }

    /// <summary>
    /// Start deployment — launches the deploy flow overlay.
    /// Vue ref: openDeployModal() → wallet picker or QR modal → success → goToMyProjects().
    /// goToMyProjects() creates the project, adds to list, closes wizard, navigates to my-projects.
    /// </summary>
    public void Deploy()
    {
        // Build the CreateProjectDto from wizard fields
        var dto = BuildCreateProjectDto();
        DeployFlow.ProjectData = dto;

        DeployFlow.OnDeployCompleted = () =>
        {
            IsDeployed = true;
            this.RaisePropertyChanged(nameof(DeployButtonText));
            // Matches Vue goToMyProjects(): add project to list + close wizard + navigate to list
            OnProjectDeployed?.Invoke();
        };
        DeployFlow.Show(ProjectName ?? "My Project");
    }

    /// <summary>
    /// Build a CreateProjectDto from all wizard fields for SDK project creation.
    /// </summary>
    private CreateProjectDto BuildCreateProjectDto()
    {
        // Parse target amount to satoshis
        var targetSats = long.TryParse(TargetAmount, out var tSats) ? tSats
            : double.TryParse(TargetAmount, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var tBtc)
                ? ((decimal)tBtc).ToUnitSatoshi()
                : 0L;

        // Map UI project type to SDK ProjectType
        var sdkProjectType = ProjectType switch
        {
            "fund" => SdkProjectType.Fund,
            "subscription" => SdkProjectType.Subscribe,
            _ => SdkProjectType.Invest
        };

        // Parse dates
        var startDt = DateTime.TryParse(StartDate, out var sd) ? sd : DateTime.UtcNow;
        DateTime? endDt = DateTime.TryParse(EndDate, out var ed) ? ed : null;

        // Build stages from the wizard's generated Stages collection
        var stageDtos = Stages.Select((s, i) =>
        {
            var pctStr = s.Percentage.Replace("%", "");
            var pct = decimal.TryParse(pctStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var p) ? p : 0m;

            // Parse the release date from the display text
            var stageDate = DateTime.TryParse(s.ReleaseDate, out var rd)
                ? DateOnly.FromDateTime(rd)
                : DateOnly.FromDateTime(startDt.AddMonths(i + 1));

            return new CreateProjectStageDto(stageDate, pct);
        }).ToList();

        // Build dynamic patterns for Fund/Subscribe types
        List<DynamicStagePattern>? patterns = null;
        int? payoutDayValue = null;

        if (sdkProjectType is SdkProjectType.Fund or SdkProjectType.Subscribe)
        {
            patterns = SelectedInstallmentCounts.Select((count, idx) =>
            {
                var freq = PayoutFrequency == "Weekly"
                    ? StageFrequency.Weekly
                    : StageFrequency.Monthly;
                var payoutDayType = PayoutFrequency == "Weekly"
                    ? PayoutDayType.SpecificDayOfWeek
                    : PayoutDayType.SpecificDayOfMonth;
                var payoutDay = PayoutFrequency == "Weekly"
                    ? ParseWeeklyPayoutDay(WeeklyPayoutDay)
                    : MonthlyPayoutDate ?? 1;

                return new DynamicStagePattern
                {
                    PatternId = (byte)idx,
                    Name = $"{count} {PayoutFrequency}",
                    Description = $"{count} {PayoutFrequency.ToLower()} payouts",
                    Frequency = freq,
                    StageCount = count,
                    PayoutDayType = payoutDayType,
                    PayoutDay = payoutDay
                };
            }).ToList();

            payoutDayValue = IsPayoutMonthly && MonthlyPayoutDate.HasValue
                ? MonthlyPayoutDate.Value
                : int.TryParse(PayoutDay, out var pd) ? pd : 1;
        }

        // For subscription, parse the subscription price as sats
        long? sats = null;
        if (sdkProjectType == SdkProjectType.Subscribe &&
            long.TryParse(SubscriptionPrice, out var subSats))
        {
            sats = subSats;
        }

        // Parse approval threshold (BTC string) to satoshis — only for Fund projects.
        // Invest projects have fixed stages and no approval threshold.
        long? penaltyThreshold = null;
        if (sdkProjectType == SdkProjectType.Fund &&
            double.TryParse(ApprovalThreshold, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var thresholdBtc) && thresholdBtc > 0)
        {
            penaltyThreshold = ((decimal)thresholdBtc).ToUnitSatoshi();
        }

        return new CreateProjectDto
        {
            ProjectName = ProjectName ?? "Untitled Project",
            Description = ProjectAbout ?? "",
            AvatarUri = ProfileUrl ?? "",
            BannerUri = BannerUrl ?? "",
            WebsiteUri = string.IsNullOrWhiteSpace(ProjectWebsite) ? null : ProjectWebsite,
            ProjectType = sdkProjectType,
            Sats = sats,
            StartDate = startDt,
            EndDate = endDt,
            TargetAmount = new Angor.Sdk.Common.Amount(targetSats),
            PenaltyDays = PenaltyDays,
            PenaltyThreshold = penaltyThreshold,
            Stages = stageDtos,
            SelectedPatterns = patterns,
            PayoutDay = payoutDayValue
        };
    }

    // ── Step 5: Payout frequency visibility (Fund/Subscription) ──
    public bool IsPayoutMonthly => PayoutFrequency == "Monthly";
    public bool IsPayoutWeekly => PayoutFrequency == "Weekly";

    // ── Step 5: Fund/Subscription — can we generate payouts? ──
    public bool CanGeneratePayouts =>
        !string.IsNullOrEmpty(PayoutFrequency) &&
        SelectedInstallmentCounts.Count > 0 &&
        ((IsPayoutMonthly && MonthlyPayoutDate.HasValue) ||
         (IsPayoutWeekly && !string.IsNullOrEmpty(WeeklyPayoutDay)));

    // ── Step 5: Investment — can we generate stages? ──
    public bool CanGenerateStages =>
        int.TryParse(DurationValue, out var dv) && dv > 0 &&
        !string.IsNullOrEmpty(ReleaseFrequency);

    /// <summary>
    /// Generate stages for Investment type based on duration + frequency.
    /// </summary>
    public void GenerateInvestmentStages()
    {
        if (!int.TryParse(DurationValue, out var durationNum) || durationNum <= 0) return;
        if (string.IsNullOrEmpty(ReleaseFrequency)) return;

        Stages.Clear();

        // Convert duration to days
        var durationDays = DurationUnit switch
        {
            "Weeks" => durationNum * 7,
            "Days" => durationNum,
            _ => durationNum * 30 // Months
        };

        // Convert frequency to days
        var frequencyDays = ReleaseFrequency switch
        {
            "Weekly" => 7,
            "Bi-Monthly" => 60,
            "Quarterly" => 120,
            _ => 30 // Monthly
        };

        var stageCount = Math.Max(1, durationDays / frequencyDays);
        var baseDate = DateTime.TryParse(StartDate, out var sd) ? sd : DateTime.UtcNow;
        var targetBtc = double.TryParse(TargetAmount, out var t) ? t : 1.0;
        var percentPerStage = 100.0 / stageCount;

        for (int i = 0; i < stageCount; i++)
        {
            var releaseDate = baseDate.AddDays((i + 1) * frequencyDays);
            var pct = i < stageCount - 1
                ? Math.Round(percentPerStage)
                : 100 - Math.Round(percentPerStage) * (stageCount - 1);
            var btcAmount = targetBtc * pct / 100;

            // Vue investment type: "Stage 1" / "8% (0.0800 BTC) released on 28th April 2026"
            Stages.Add(new ProjectStageViewModel
            {
                StageNumber = i + 1,
                Percentage = $"{pct}%",
                ReleaseDate = FormatReleaseDateOrdinal(releaseDate),
                AmountBtc = btcAmount.ToString("F4"),
                StageLabel = "Stage",
                DisplayText = $"{pct}% ({btcAmount:F4} {_currencyService.Symbol}) released on {FormatReleaseDateOrdinal(releaseDate)}"
            });
        }

        ShowGenerateForm = false; // collapse the generate form, show results
        this.RaisePropertyChanged(nameof(HasStages));
        this.RaisePropertyChanged(nameof(CanGoNext));
        this.RaisePropertyChanged(nameof(ScheduleSummary));
    }

    /// <summary>
    /// Generate payouts for Fund/Subscription type based on frequency + installments + day.
    /// </summary>
    public void GeneratePayoutSchedule()
    {
        if (SelectedInstallmentCounts.Count == 0) return;
        if (string.IsNullOrEmpty(PayoutFrequency)) return;

        Stages.Clear();

        // Vue: uses max of selected installment counts as the number of payouts
        var count = SelectedInstallmentCounts.Max();
        var baseDate = DateTime.TryParse(StartDate, out var sd) ? sd : DateTime.UtcNow;
        var targetBtc = double.TryParse(TargetAmount, out var t) ? t : 1.0;

        // For subscription, use SubscriptionPrice as BTC
        if (ProjectType == "subscription" && double.TryParse(SubscriptionPrice, out var subBtc))
            targetBtc = subBtc;

        var percentPerStage = 100.0 / count;

        var pattern = new DynamicStagePattern
        {
            Frequency = PayoutFrequency == "Weekly" ? StageFrequency.Weekly : StageFrequency.Monthly,
            StageCount = count,
            PayoutDayType = PayoutFrequency == "Weekly"
                ? PayoutDayType.SpecificDayOfWeek
                : PayoutDayType.SpecificDayOfMonth,
            PayoutDay = PayoutFrequency == "Weekly"
                ? ParseWeeklyPayoutDay(WeeklyPayoutDay)
                : MonthlyPayoutDate ?? 1
        };

        for (int i = 0; i < count; i++)
        {
            var releaseDate = DynamicStageCalculator.CalculateDynamicStageReleaseDate(baseDate, pattern, i);

            var pct = i < count - 1
                ? Math.Round(percentPerStage)
                : 100 - Math.Round(percentPerStage) * (count - 1);

            var label = PayoutFrequency == "Weekly" ? "Weekly Payout" : "Monthly Payout";

            // Vue fund/subscription: "33% paid on 28th April 2026"
            Stages.Add(new ProjectStageViewModel
            {
                StageNumber = i + 1,
                Percentage = $"{pct}%",
                ReleaseDate = FormatReleaseDateOrdinal(releaseDate),
                AmountBtc = (targetBtc * pct / 100).ToString("F4"),
                StageLabel = label,
                DisplayText = $"{pct}% paid on {FormatReleaseDateOrdinal(releaseDate)}"
            });
        }

        ShowGenerateForm = false; // collapse the generate form, show results
        this.RaisePropertyChanged(nameof(HasStages));
        this.RaisePropertyChanged(nameof(CanGoNext));
        this.RaisePropertyChanged(nameof(ScheduleSummary));
    }

    /// <summary>Toggle an installment count in the multiselect list. Vue: toggleInstallmentCount().</summary>
    public void ToggleInstallmentCount(int count)
    {
        if (SelectedInstallmentCounts.Contains(count))
            SelectedInstallmentCounts.Remove(count);
        else
            SelectedInstallmentCounts.Add(count);

        // Keep sorted
        var sorted = SelectedInstallmentCounts.OrderBy(x => x).ToList();
        SelectedInstallmentCounts.Clear();
        foreach (var c in sorted)
            SelectedInstallmentCounts.Add(c);

        this.RaisePropertyChanged(nameof(CanGeneratePayouts));
        this.RaisePropertyChanged(nameof(CanGoNext));
    }

    /// <summary>Clear all generated stages/payouts and re-show the generate form.</summary>
    public void ClearStages()
    {
        Stages.Clear();
        ShowGenerateForm = true;
        this.RaisePropertyChanged(nameof(HasStages));
        this.RaisePropertyChanged(nameof(CanGoNext));
    }

    private static int ParseWeeklyPayoutDay(string weeklyPayoutDay)
    {
        if (Enum.TryParse<DayOfWeek>(weeklyPayoutDay, true, out var parsedDay))
        {
            return (int)parsedDay;
        }

        return weeklyPayoutDay.Trim().ToLowerInvariant() switch
        {
            "sun" => (int)DayOfWeek.Sunday,
            "mon" => (int)DayOfWeek.Monday,
            "tue" => (int)DayOfWeek.Tuesday,
            "wed" => (int)DayOfWeek.Wednesday,
            "thu" => (int)DayOfWeek.Thursday,
            "fri" => (int)DayOfWeek.Friday,
            "sat" => (int)DayOfWeek.Saturday,
            _ => 1
        };
    }

    /// <summary>Toggle between simple and advanced editor (Investment).</summary>
    public void ToggleAdvancedEditor()
    {
        IsAdvancedEditor = !IsAdvancedEditor;
    }

    /// <summary>Re-show the generate form to regenerate stages (without clearing existing ones first).</summary>
    public void ShowRegenerateForm()
    {
        ShowGenerateForm = true;
    }

    // ── Stages (generated from payout pattern) ──
    public ObservableCollection<ProjectStageViewModel> Stages { get; } = new();

    public bool HasStages => Stages.Count > 0;

    // ── Generate button labels ──
    public string GenerateButtonLabel => TypeEnum switch
    {
        Shared.ProjectType.Fund or Shared.ProjectType.Subscription => "Generate Payout Schedule",
        _ => "Generate Fund Release Schedule"
    };

    public string Step5Subtitle => TypeEnum switch
    {
        Shared.ProjectType.Fund or Shared.ProjectType.Subscription => "Select your payout pattern and schedule",
        _ => "Tell us how long the project will take and when you need payments"
    };

    /// <summary>Duration unit options for the ComboBox dropdown.</summary>
    public string[] DurationUnitOptions => ["Days", "Weeks", "Months"];

    /// <summary>
    /// Dynamic preset items that change based on the selected DurationUnit.
    /// Vue: months→(3,6,12,18,24), weeks→(2,4,6,8,12), days→(3,7,14,21,28,30)
    /// Each item has a Value (int) and Label (e.g. "3 Months").
    /// </summary>
    public DurationPresetItem[] DurationPresetItems => DurationUnit switch
    {
        "Weeks" => [new(2, "2 Weeks"), new(4, "4 Weeks"), new(6, "6 Weeks"), new(8, "8 Weeks"), new(12, "12 Weeks")],
        "Days" => [new(3, "3 Days"), new(7, "7 Days"), new(14, "14 Days"), new(21, "21 Days"), new(28, "28 Days"), new(30, "30 Days")],
        _ => [new(3, "3 Months"), new(6, "6 Months"), new(12, "12 Months"), new(18, "18 Months"), new(24, "24 Months")]
    };

    /// <summary>
    /// Format a DateTime with ordinal suffix and full month name.
    /// e.g. "28th April 2026", "1st March 2026", "3rd January 2027"
    /// Matches Vue formatReleaseDate() function.
    /// </summary>
    private static string FormatReleaseDateOrdinal(DateTime date)
    {
        var day = date.Day;
        var suffix = (day % 10 == 1 && day != 11) ? "st" :
                     (day % 10 == 2 && day != 12) ? "nd" :
                     (day % 10 == 3 && day != 13) ? "rd" : "th";
        return $"{day}{suffix} {date:MMMM yyyy}";
    }

    /// <summary>
    /// Summary text shown in the Step 6 stages header (right side).
    /// E.g. "Monthly payouts (3 installments)" or "Quarterly releases (4 stages)".
    /// </summary>
    public string ScheduleSummary
    {
        get
        {
            if (Stages.Count == 0) return "";
            if (IsInvestment)
            {
                var freq = ReleaseFrequency;
                if (string.IsNullOrEmpty(freq)) freq = "Monthly";
                return $"{freq} releases ({Stages.Count} stages)";
            }
            else
            {
                var freq = PayoutFrequency;
                if (string.IsNullOrEmpty(freq)) freq = "Monthly";
                return $"{freq} payouts ({Stages.Count} installments)";
            }
        }
    }

    private void GenerateStages()
    {
        Stages.Clear();

        var (count, frequencyMonths) = SelectedPayoutPattern switch
        {
            "pattern1" => (3, 1),   // 3 monthly
            "pattern2" => (6, 1),   // 6 monthly
            "pattern3" => ProjectType == "subscription" ? (12, 1) : (6, 0), // 12 monthly or 6 weekly
            "pattern4" => (9, 0),   // 9 weekly
            _ => (3, 1)
        };

        var baseDate = DateTime.TryParse(StartDate, out var sd) ? sd : DateTime.UtcNow;
        var percentPerStage = 100.0 / count;
        var targetBtc = double.TryParse(TargetAmount, out var t) ? t : 1.0;

        for (int i = 0; i < count; i++)
        {
            var releaseDate = frequencyMonths > 0
                ? baseDate.AddMonths((i + 1) * frequencyMonths)
                : baseDate.AddDays((i + 1) * 7); // weekly

            var pct = i < count - 1
                ? Math.Round(percentPerStage)
                : 100 - Math.Round(percentPerStage) * (count - 1);

            var label = frequencyMonths > 0 ? "Monthly Payout" : "Weekly Payout";

            Stages.Add(new ProjectStageViewModel
            {
                StageNumber = i + 1,
                Percentage = $"{pct}%",
                ReleaseDate = FormatReleaseDateOrdinal(releaseDate),
                AmountBtc = (targetBtc * pct / 100).ToString("F4"),
                StageLabel = label,
                DisplayText = $"{pct}% paid on {FormatReleaseDateOrdinal(releaseDate)}"
            });
        }

        this.RaisePropertyChanged(nameof(CanGoNext));
        this.RaisePropertyChanged(nameof(HasStages));
        this.RaisePropertyChanged(nameof(ScheduleSummary));
    }

    /// <summary>
    /// Reset all wizard state to initial values so the wizard can be re-opened fresh.
    /// Called by MyProjectsView.OpenCreateWizard() before wiring callbacks.
    /// </summary>
    public void ResetWizard()
    {
        // Navigation state
        CurrentStep = 1;
        MaxStepReached = 1;
        ShowWelcome = true;
        ShowStep5Welcome = true;

        // Step 1: Project Type
        ProjectType = "";
        this.RaisePropertyChanged(nameof(IsInvestment));
        this.RaisePropertyChanged(nameof(IsFund));
        this.RaisePropertyChanged(nameof(IsSubscription));
        this.RaisePropertyChanged(nameof(IsTypeSelected));
        this.RaisePropertyChanged(nameof(StepNames));

        // Step 2: Project Profile
        ProjectName = "";
        ProjectAbout = "";
        ProjectWebsite = "";

        // Clear validation errors
        ClearErrors();

        // Step 3: Project Images
        BannerUrl = "";
        ProfileUrl = "";

        // Step 4: Funding Config
        TargetAmount = "";
        StartDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
        EndDate = "";
        PenaltyDays = 90;
        ApprovalThreshold = "0.001";
        SubscriptionPrice = "";
        InvestStartDate = DateTime.Now;
        InvestEndDate = null;

        // Step 5: Stages/Payouts
        DurationValue = "0";
        DurationUnit = "Months";
        DurationPreset = null;
        ReleaseFrequency = "";
        PayoutFrequency = "";
        MonthlyPayoutDate = null;
        WeeklyPayoutDay = "";
        SelectedInstallmentCounts.Clear();
        IsAdvancedEditor = false;
        ShowGenerateForm = true;
        SelectedPayoutPattern = "";
        PayoutDay = "1";

        // Step 5: Clear generated stages
        Stages.Clear();
        this.RaisePropertyChanged(nameof(HasStages));
        this.RaisePropertyChanged(nameof(ScheduleSummary));

        // Step 6: Deploy
        IsDeploying = false;
        IsDeployed = false;
        this.RaisePropertyChanged(nameof(DeployButtonText));

        // Clear callback
        OnProjectDeployed = null;

        // Notify all step visibility properties
        RaiseAllStepProperties();

        // Re-evaluate debug mode (may have changed in settings since last open)
        this.RaisePropertyChanged(nameof(IsDebugMode));
    }    /// <summary>
    /// Prepopulate all wizard fields with debug/test data so the user can
    /// click Next through every step and deploy quickly on testnet.
    /// Values mirror the Avalonia app's PopulateInvestDebugDefaults / PopulateFundDebugDefaults.
    /// </summary>
    public void PrepopulateDebugData()
    {
        if (!IsDebugMode) return;

        var id = Guid.NewGuid().ToString()[..8];

        // If no project type is selected yet, default to investment
        if (string.IsNullOrEmpty(ProjectType))
        {
            SelectProjectType("investment");
        }

        if (ProjectType == "fund")
        {
            PrepopulateFundDefaults(id);
        }
        else if (ProjectType == "subscription")
        {
            PrepopulateSubscriptionDefaults(id);
        }
        else
        {
            PrepopulateInvestmentDefaults(id);
        }

        // Step 3: Images — random placeholder images (same approach as Avalonia app's DebugData)
        var seed = Guid.NewGuid().ToString("N")[..8];
        BannerUrl = $"https://picsum.photos/seed/{seed}/820/312";
        ProfileUrl = $"https://picsum.photos/seed/{seed}/170/170";

        ClearErrors();
        this.RaisePropertyChanged(nameof(CanGoNext));
    }

    private void PrepopulateInvestmentDefaults(string id)
    {
        // Step 2: Profile
        ProjectName = $"Debug Project {id}";
        ProjectAbout = $"Auto-populated debug project {id} for testing on testnet. Created at {DateTime.Now:HH:mm:ss}.";
        ProjectWebsite = "https://angor.io";

        // Step 4: Funding config
        TargetAmount = "0.01";
        PenaltyDays = 0;
        InvestStartDate = DateTime.Now.Date;
        InvestEndDate = DateTime.Now.Date;

        // Step 5: Stages — 3 stages released today (10%, 30%, 60%)
        Stages.Clear();
        var today = DateTime.Now.Date;
        var targetBtc = 0.01;

        Stages.Add(new ProjectStageViewModel
        {
            StageNumber = 1,
            Percentage = "10%",
            ReleaseDate = FormatReleaseDateOrdinal(today),
            AmountBtc = (targetBtc * 0.10).ToString("F4"),
            StageLabel = "Stage",
            DisplayText = $"10% ({targetBtc * 0.10:F4} {_currencyService.Symbol}) released on {FormatReleaseDateOrdinal(today)}"
        });
        Stages.Add(new ProjectStageViewModel
        {
            StageNumber = 2,
            Percentage = "30%",
            ReleaseDate = FormatReleaseDateOrdinal(today),
            AmountBtc = (targetBtc * 0.30).ToString("F4"),
            StageLabel = "Stage",
            DisplayText = $"30% ({targetBtc * 0.30:F4} {_currencyService.Symbol}) released on {FormatReleaseDateOrdinal(today)}"
        });
        Stages.Add(new ProjectStageViewModel
        {
            StageNumber = 3,
            Percentage = "60%",
            ReleaseDate = FormatReleaseDateOrdinal(today),
            AmountBtc = (targetBtc * 0.60).ToString("F4"),
            StageLabel = "Stage",
            DisplayText = $"60% ({targetBtc * 0.60:F4} {_currencyService.Symbol}) released on {FormatReleaseDateOrdinal(today)}"
        });

        ShowGenerateForm = false;
        this.RaisePropertyChanged(nameof(HasStages));
        this.RaisePropertyChanged(nameof(ScheduleSummary));
    }

    private void PrepopulateFundDefaults(string id)
    {
        // Step 2: Profile
        ProjectName = $"Debug Fund {id}";
        ProjectAbout = $"Auto-populated debug fund {id} for testing on testnet. Created at {DateTime.Now:HH:mm:ss}.";
        ProjectWebsite = "https://angor.io";

        // Step 4: Funding config
        TargetAmount = "0.5";
        ApprovalThreshold = "0.01";
        PenaltyDays = 0;

        // Step 5: Payouts — Monthly, day = today, installments 3 and 6
        PayoutFrequency = "Monthly";
        MonthlyPayoutDate = DateTime.Now.Day;
        SelectedInstallmentCounts.Clear();
        SelectedInstallmentCounts.Add(3);
        SelectedInstallmentCounts.Add(6);
        this.RaisePropertyChanged(nameof(IsPayoutMonthly));
        this.RaisePropertyChanged(nameof(IsPayoutWeekly));
        this.RaisePropertyChanged(nameof(CanGeneratePayouts));

        // Generate payout stages
        GeneratePayoutSchedule();
    }

    private void PrepopulateSubscriptionDefaults(string id)
    {
        // Step 2: Profile
        ProjectName = $"Debug Subscription {id}";
        ProjectAbout = $"Auto-populated debug subscription {id} for testing on testnet. Created at {DateTime.Now:HH:mm:ss}.";
        ProjectWebsite = "https://angor.io";

        // Step 4: Subscription price
        SubscriptionPrice = "0.0001";

        // Step 5: Payouts — Monthly, day = today, installments 3 and 6
        PayoutFrequency = "Monthly";
        MonthlyPayoutDate = DateTime.Now.Day;
        SelectedInstallmentCounts.Clear();
        SelectedInstallmentCounts.Add(3);
        SelectedInstallmentCounts.Add(6);
        this.RaisePropertyChanged(nameof(IsPayoutMonthly));
        this.RaisePropertyChanged(nameof(IsPayoutWeekly));
        this.RaisePropertyChanged(nameof(CanGeneratePayouts));

        // Generate payout stages
        GeneratePayoutSchedule();
    }

    private void ClearErrors()
    {
        FormError = "";
        NameError = "";
        AboutError = "";
        TargetAmountError = "";
        EndDateError = "";
        StagesError = "";
        RaiseErrorProperties();
    }

    private void RaiseErrorProperties()
    {
        this.RaisePropertyChanged(nameof(HasFormError));
        this.RaisePropertyChanged(nameof(HasNameError));
        this.RaisePropertyChanged(nameof(HasAboutError));
        this.RaisePropertyChanged(nameof(HasTargetAmountError));
        this.RaisePropertyChanged(nameof(HasEndDateError));
        this.RaisePropertyChanged(nameof(HasStagesError));
    }

    private void RaiseAllStepProperties()
    {
        this.RaisePropertyChanged(nameof(IsWelcome));
        this.RaisePropertyChanged(nameof(IsStep1));
        this.RaisePropertyChanged(nameof(IsStep2));
        this.RaisePropertyChanged(nameof(IsStep3));
        this.RaisePropertyChanged(nameof(IsStep4));
        this.RaisePropertyChanged(nameof(IsStep5));
        this.RaisePropertyChanged(nameof(IsStep5Welcome));
        this.RaisePropertyChanged(nameof(IsStep5Form));
        this.RaisePropertyChanged(nameof(IsStep6));
        this.RaisePropertyChanged(nameof(ShowNavFooter));
        this.RaisePropertyChanged(nameof(ShowScrollContent));
        this.RaisePropertyChanged(nameof(BackButtonText));
        this.RaisePropertyChanged(nameof(CanGoNext));
        this.RaisePropertyChanged(nameof(CanGoBack));
        this.RaisePropertyChanged(nameof(IsLastStep));
    }
}
