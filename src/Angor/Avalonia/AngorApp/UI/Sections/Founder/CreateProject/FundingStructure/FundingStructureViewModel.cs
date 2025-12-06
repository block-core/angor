using System.Reactive.Disposables;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Angor.Shared.Models;
using System.Linq;
using ReactiveUI;
using System.Collections.ObjectModel;

namespace AngorApp.UI.Sections.Founder.CreateProject.FundingStructure;

public partial class FundingStructureViewModel : ReactiveValidationObject, IFundingStructureViewModel, IHaveErrors
{
    [Reactive] private ProjectType projectType = ProjectType.Invest;
    [Reactive] private long? sats;
    [Reactive] private int? penaltyDays = 100;
    [Reactive] private long? penaltyThreshold;
    [Reactive] private DateTime? fundingEndDate;
    [Reactive] private DateTime? expiryDate;
    [Reactive] private int? payoutDay;
    [ObservableAsProperty] private IAmountUI targetAmount;

    private readonly CompositeDisposable disposable = new();
    private bool enableProductionValidations;

    public FundingStructureViewModel(UIServices uiServices)
    {
        // Initialize selectable patterns
        SelectableDynamicStagePatterns = new ObservableCollection<SelectableDynamicStagePattern>();
        SelectedPatterns = new ObservableCollection<DynamicStagePattern>();

        LoadPatternsForProjectType();

        AddValidations(uiServices);

        targetAmountHelper = this.WhenAnyValue(x => x.Sats)
            .WhereNotNull()
            .Select(l => new AmountUI(l.Value))
            .ToProperty(this, model => model.TargetAmount)
            .DisposeWith(disposable);

        // When project type changes, reload patterns and reset selections
        this.WhenAnyValue(x => x.ProjectType)
        .Subscribe(_ =>
        {
            LoadPatternsForProjectType();
            // Clear previous selections when project type changes
            SelectedPatterns.Clear();
        })
        .DisposeWith(disposable);

        Errors = new ErrorSummarizer(ValidationContext).DisposeWith(disposable).Errors;
    }

    private void LoadPatternsForProjectType()
    {
        SelectableDynamicStagePatterns.Clear();

        if (ProjectType == ProjectType.Fund)
        {
            // For Fund, offer 3-month and 6-month patterns
            var patterns = DynamicStagePattern.GetMonthlyPatterns();
            var pattern3Month = new SelectableDynamicStagePattern(patterns.First(p => p.StageCount == 3));
            var pattern6Month = new SelectableDynamicStagePattern(patterns.First(p => p.StageCount == 6));

            // Subscribe to IsSelected changes to update SelectedPatterns
            pattern3Month.WhenAnyValue(x => x.IsSelected)
                .Subscribe(_ => UpdateSelectedPatterns())
                .DisposeWith(disposable);

            pattern6Month.WhenAnyValue(x => x.IsSelected)
                .Subscribe(_ => UpdateSelectedPatterns())
                .DisposeWith(disposable);

            SelectableDynamicStagePatterns.Add(pattern3Month);
            SelectableDynamicStagePatterns.Add(pattern6Month);
        }
        else if (ProjectType == ProjectType.Subscribe)
        {
            // For Subscribe, we'll add patterns later when enabled
            // For now, keep empty
        }
    }

    private void UpdateSelectedPatterns()
    {
        SelectedPatterns.Clear();
        foreach (var selectable in SelectableDynamicStagePatterns.Where(p => p.IsSelected))
        {
            SelectedPatterns.Add(selectable.Pattern);
        }
    }

    private void AddValidations(UIServices uiServices)
    {
        enableProductionValidations = uiServices.EnableProductionValidations();

        // ============================================================
        // INVEST TYPE VALIDATIONS
        // ============================================================
        // Required: TargetAmount, EndDate, Sats, PenaltyDays, PenaltyThreshold, Stages

        // Target amount - REQUIRED for Invest, optional for Fund/Subscribe
        var isSatsValid = this.WhenAnyValue(x => x.ProjectType, x => x.Sats, (projType, sats) => projType != ProjectType.Invest || sats is not null);
        this.ValidationRule(x => x.Sats, isSatsValid, "Target amount is required for Investment projects.").DisposeWith(disposable);

        this.ValidationRule(x => x.Sats, x => x is null or > 0, _ => "Target amount must be greater than 0.").DisposeWith(disposable);

        // Funding end date - REQUIRED for Invest, NOT used for Fund/Subscribe
        var isEndDateValid = this.WhenAnyValue(x => x.ProjectType, x => x.FundingEndDate, (projType, endDate) => projType != ProjectType.Invest || endDate != null);
        this.ValidationRule(x => x.FundingEndDate, isEndDateValid, "Funding end date is required for Investment projects.").DisposeWith(disposable);

        // ============================================================
        // FUND TYPE VALIDATIONS
        // ============================================================
        // Required: SelectedPatterns (DynamicStagePatterns), PenaltyDays, PenaltyThreshold
        // StartDate is always set, ExpiryDate calculated as StartDate.AddMonths(6)

        // Pattern validation - REQUIRED for Fund and Subscribe types
        var isPatternValid = this.WhenAnyValue(x => x.ProjectType, x => x.SelectedPatterns.Count, (projType, count) => projType == ProjectType.Invest || count > 0);
        this.ValidationRule(x => x.SelectedPatterns, isPatternValid, "Please select at least one funding pattern for Fund projects.").DisposeWith(disposable);

        // Payout day - optional for Fund/Subscribe but should be valid if provided
        var isPayoutDayValid = this.WhenAnyValue(x => x.ProjectType, x => x.PayoutDay, (projType, day) => projType == ProjectType.Invest || !day.HasValue || (day.Value >= 1 && day.Value <= 31));
        this.ValidationRule(x => x.PayoutDay, isPayoutDayValid, "Payout day must be between 1 and 31 if specified.").DisposeWith(disposable);

        // ============================================================
        // SUBSCRIBE TYPE VALIDATIONS
        // ============================================================
        // Required: SelectedPatterns (DynamicStagePatterns)
        // No PenaltyDays or PenaltyThreshold for Subscribe

        // Penalty days - REQUIRED for Invest/Fund, NOT used for Subscribe
        var isPenaltyDaysValid = this.WhenAnyValue(x => x.ProjectType, x => x.PenaltyDays,
            (projType, days) => projType == ProjectType.Subscribe || days.HasValue);
        this.ValidationRule(x => x.PenaltyDays, isPenaltyDaysValid, "Penalty days are required for Investment and Fund projects.").DisposeWith(disposable);

        this.ValidationRule(x => x.PenaltyDays, x => x is null || x >= 0, "Penalty days cannot be negative.").DisposeWith(disposable);
        this.ValidationRule(x => x.PenaltyDays, x => x is null || x <= 365, "Penalty period cannot exceed 365 days.").DisposeWith(disposable);

        // Penalty threshold - optional but if provided should be valid
        this.ValidationRule(x => x.PenaltyThreshold, x => x is null or >= 0, "Penalty threshold must be greater than or equal to 0.").DisposeWith(disposable);

        // ============================================================
        // PRODUCTION VALIDATIONS
        // ============================================================
        if (enableProductionValidations)
        {
            // Debug Mode OFF

            // Min 0.001 BTC (100,000 sats), Max 100 BTC (10,000,000,000 sats) - for Invest only
            var isSatsProductionValid = this.WhenAnyValue(x => x.ProjectType, x => x.Sats, (projType, sats) => projType != ProjectType.Invest || sats is null || sats >= 100_000);
            this.ValidationRule(x => x.Sats, isSatsProductionValid, "Target amount must be at least 0.001 BTC for Investment projects.").DisposeWith(disposable);

            this.ValidationRule(x => x.Sats, x => x is null || x <= 10_000_000_000, _ => "Target amount cannot exceed 100 BTC.").DisposeWith(disposable);

            // Penalty days minimum for Invest/Fund
            var isPenaltyDaysProductionValid = this.WhenAnyValue(x => x.ProjectType, x => x.PenaltyDays, (projType, days) => projType == ProjectType.Subscribe || days is null || days >= 10);
            this.ValidationRule(x => x.PenaltyDays, isPenaltyDaysProductionValid, "Penalty period must be at least 10 days for Investment and Fund projects.").DisposeWith(disposable);

            // Funding period cannot exceed 1 year (only for Invest)
            var isFundingPeriodValid = this.WhenAnyValue(x => x.ProjectType, x => x.FundingEndDate, (projType, time) => projType != ProjectType.Invest || time == null || time.Value - FundingStartDate <= TimeSpan.FromDays(365));
            this.ValidationRule(x => x.FundingEndDate, isFundingPeriodValid, "Funding period cannot exceed one year.").DisposeWith(disposable);

            var isEndDateAfterStart = this.WhenAnyValue(x => x.ProjectType, x => x.FundingEndDate, (projType, time) => projType != ProjectType.Invest || time?.Date > FundingStartDate.Date);
            this.ValidationRule(x => x.FundingEndDate, isEndDateAfterStart, "Funding end date must be after the start date.").DisposeWith(disposable);
        }
        else
        {
            // Debug Mode ON

            // Allow end date to be equal to or after start date (only for Invest)
            var isEndDateValidDebug = this.WhenAnyValue(x => x.ProjectType, x => x.FundingEndDate, (projType, time) => projType != ProjectType.Invest || time?.Date >= FundingStartDate.Date);
            this.ValidationRule(x => x.FundingEndDate, isEndDateValidDebug, "Funding end date must be on or after the funding start date.").DisposeWith(disposable);
        }
    }

    protected override void Dispose(bool disposing)
    {
        disposable.Dispose();
        base.Dispose(disposing);
    }

    public IObservable<bool> IsValid => this.IsValid();
    public DateTime FundingStartDate { get; } = DateTime.Now;
    public List<DynamicStagePattern> DynamicStagePatterns => SelectableDynamicStagePatterns.Select(s => s.Pattern).ToList();
    public ObservableCollection<SelectableDynamicStagePattern> SelectableDynamicStagePatterns { get; }
    public ObservableCollection<DynamicStagePattern> SelectedPatterns { get; }
    public ICollection<string> Errors { get; }
}