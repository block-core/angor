using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Model;
using DynamicData;
using DynamicData.Aggregation;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model
{
    public abstract partial class InvestmentProjectConfigBase : ReactiveValidationObject, IInvestmentProjectConfig, IDisposable
    {
        protected enum ValidationEnvironment
        {
            Production,
            Debug
        }

        private readonly ReadOnlyObservableCollection<IFundingStageConfig> stages;
        protected readonly CompositeDisposable Disposables = new();
        [Reactive] private string name = string.Empty;
        [Reactive] private string description = string.Empty;
        [Reactive] private string website = string.Empty;


        [Reactive] private IAmountUI? targetAmount;
        [Reactive] private int? penaltyDays;
        [Reactive] private long? penaltyThreshold;
        [Reactive] private DateTime? fundingEndDate;
        [Reactive] private DateTime? startDate;
        [Reactive] private DateTime? expiryDate;

        [Reactive] private string avatarUri = DebugData.GetDefaultImageUriString(170, 170);
        [Reactive] private string bannerUri = DebugData.GetDefaultImageUriString(820, 312);
        
        [Reactive] private string nip05 = string.Empty;
        [Reactive] private string lud16 = string.Empty;
        [Reactive] private string nip57 = string.Empty;

        [Reactive] private Angor.Shared.Models.ProjectType projectType = Angor.Shared.Models.ProjectType.Invest;

        public SourceList<IFundingStageConfig> StagesSource { get; } = new();
        public ReadOnlyObservableCollection<IFundingStageConfig> Stages => stages;

        protected InvestmentProjectConfigBase(ValidationEnvironment environment)
        {
            StagesSource.Connect()
                .Bind(out stages)
                .Subscribe()
                .DisposeWith(Disposables);

            StagesSource.Connect()
                .ToCollection()
                .Subscribe(RewireStageLinks)
                .DisposeWith(Disposables);

            var totalPercent = StagesSource.Connect()
                .AutoRefresh(x => x.Percent)
                .ToCollection()
                .Select(items => items.Sum(x => x.Percent ?? 0));


            this.ValidationRule(x => x.Name, x => !string.IsNullOrWhiteSpace(x), "Project name is required.").DisposeWith(Disposables);
            this.ValidationRule(x => x.Description, x => !string.IsNullOrWhiteSpace(x), "Project description is required.").DisposeWith(Disposables);
            this.ValidationRule(x => x.Website, x => string.IsNullOrWhiteSpace(x) || (Uri.TryCreate(x, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)), "Website must be a valid URL (http or https).").DisposeWith(Disposables);

            var isTotalPercentValid = totalPercent.Select(percent => Math.Abs(percent - 1.0m) < 0.0001m);
            this.ValidationRule(x => x.Stages, isTotalPercentValid, "Total percentage must be 100%");


            this.ValidationRule(
                this.WhenAnyValue(
                    x => x.TargetAmount,
                    x => x.TargetAmount!.Sats,
                    (amount, sats) => amount != null && sats > 0
                ),
                isValid => isValid,
                _ => "Target amount must be greater than 0.")
                .DisposeWith(Disposables);
            this.ValidationRule(x => x.TargetAmount, x => x != null, _ => "Target amount is required.").DisposeWith(Disposables);


            this.ValidationRule(x => x.PenaltyDays, x => x != null, "Penalty days is required.").DisposeWith(Disposables);
            this.ValidationRule(x => x.PenaltyDays, x => x is null || x >= 0, "Penalty days cannot be negative.").DisposeWith(Disposables);
            this.ValidationRule(x => x.PenaltyDays, x => x is null || x <= 365, "Penalty period cannot exceed 365 days.").DisposeWith(Disposables);


            this.ValidationRule(x => x.PenaltyThreshold, x => x is null or >= 0, "Penalty threshold must be greater than or equal to 0.").DisposeWith(Disposables);


            this.ValidationRule(x => x.FundingEndDate, x => x != null, "Funding end date is required.").DisposeWith(Disposables);


            var areStagesValid = StagesSource.Connect().FilterOnObservable(stage => stage.IsValid).IsEmpty().Select(b => !b);
            this.ValidationRule(x => x.Stages, areStagesValid, "Stages are not valid").DisposeWith(Disposables);

            this.ValidationRule(x => x.StartDate, x => x != null, "Start date is required.").DisposeWith(Disposables);

            this.ValidationRule(
                this.WhenAnyValue(x => x.StartDate, x => x.FundingEndDate, (start, end) => new { Start = start, End = end }),
                dates => !dates.Start.HasValue || !dates.End.HasValue || dates.Start.Value <= dates.End.Value,
                _ => "Start date must be before or equal to funding end date."
            ).DisposeWith(Disposables);


            AddEnvironmentSpecificValidations(environment);
        }

        private void AddEnvironmentSpecificValidations(ValidationEnvironment environment)
        {
            if (environment == ValidationEnvironment.Production)
            {
                AddProductionValidations();
            }
            else
            {
                AddDebugValidations();
            }
        }

        private void AddProductionValidations()
        {

            this.ValidationRule(
                this.WhenAnyValue(
                    x => x.TargetAmount,
                    x => x.TargetAmount!.Sats,
                    (amount, sats) => amount == null || sats >= 100_000
                ),
                isValid => isValid,
                _ => "Target amount must be at least 0.001 BTC.")
                .DisposeWith(Disposables);
            this.ValidationRule(
                this.WhenAnyValue(
                    x => x.TargetAmount,
                    x => x.TargetAmount!.Sats,
                    (amount, sats) => amount == null || sats <= 10_000_000_000
                ),
                isValid => isValid,
                _ => "Target amount cannot exceed 100 BTC.")
                .DisposeWith(Disposables);


            this.ValidationRule(x => x.PenaltyDays, x => x is null || x >= 10, "Penalty period must be at least 10 days.").DisposeWith(Disposables);


            this.ValidationRule(x => x.FundingEndDate, x => x == null || x.Value.Date > DateTime.Now.Date, "Funding end date must be after today.").DisposeWith(Disposables);
            this.ValidationRule(x => x.FundingEndDate, x => x == null || (x.Value - DateTime.Now) <= TimeSpan.FromDays(365), "Funding period cannot exceed one year.").DisposeWith(Disposables);
        }

        private void AddDebugValidations()
        {
            this.ValidationRule(x => x.FundingEndDate, x => x == null || x.Value.Date >= DateTime.Now.Date, "Funding end date must be on or after today.").DisposeWith(Disposables);
        }

        public void AddStage()
        {
            StagesSource.Add(new FundingStageConfig());
        }

        public IFundingStageConfig CreateAndAddStage(decimal percent = 0, DateTime? releaseDate = null)
        {
            AddStage();
            var stage = StagesSource.Items.Last();
            stage.Percent = percent;
            if (releaseDate.HasValue)
            {
                stage.ReleaseDate = releaseDate;
            }

            return stage;
        }

        public void RemoveStage(IFundingStageConfig stage)
        {
            StagesSource.Remove(stage);
            (stage as IDisposable)?.Dispose();
        }

        public new void Dispose()
        {
            Disposables.Dispose();
            StagesSource.Dispose();
            base.Dispose();
        }

        private void RewireStageLinks(IReadOnlyCollection<IFundingStageConfig> snapshot)
        {
            var list = snapshot.ToList();

            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] is not { } stage)
                {
                    continue;
                }

                IObservable<DateTime> previousDate =
                    i == 0
                        ? this.WhenAnyValue(x => x.FundingEndDate).Select(d => d ?? DateTime.MinValue)
                        : list[i - 1].WhenAnyValue(x => x.ReleaseDate).Select(d => d ?? DateTime.MinValue);

                stage.SetPreviousDateSource(previousDate);
            }
        }

        public IObservable<bool> IsValid => this.IsValid();
    }
}
