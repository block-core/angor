using System.Linq;
using Angor.Sdk.Funding.Investor;
using Angor.Shared.Models;
using AngorApp.UI.Flows.InvestV2.Footer;
using AngorApp.UI.Flows.InvestV2.Header;
using AngorApp.UI.Flows.InvestV2.Model;
using AngorApp.UI.Shell;
using ReactiveUI.Validation.Helpers;
using Zafiro.Reactive;

namespace AngorApp.UI.Flows.InvestV2
{
    public partial class InvestViewModel : ReactiveValidationObject, IInvestViewModel, IValidatable
    {
        private readonly IFullProject fullProject;
        private readonly IShellViewModel shell;
        private readonly UIServices uiServices;
        private readonly IWalletContext walletContext;
        private readonly IInvestmentAppService investmentAppService;
        [Reactive] private IAmountUI? amountToInvest;
        [Reactive] private DynamicStagePattern? selectedPattern;
        
        public InvestViewModel(
            IFullProject fullProject,
            UIServices uiServices,
            IShellViewModel shell,
            IInvestmentAppService investmentAppService,
            IWalletContext walletContext
        )
        {
            this.fullProject = fullProject;
            this.uiServices = uiServices;
            this.shell = shell;
            this.investmentAppService = investmentAppService;
            this.walletContext = walletContext;

            // Initialize pattern selection for Fund/Subscribe projects
            var patterns = fullProject.DynamicStagePatterns ?? new List<DynamicStagePattern>();
            AvailablePatterns = patterns;
            ShowPatternSelector = fullProject.ProjectType is ProjectType.Fund or ProjectType.Subscribe
                                  && patterns.Count > 0;
            if (ShowPatternSelector)
            {
                SelectedPattern = patterns.First();
            }

            StageBreakdowns = this.WhenAnyValue(model => model.AmountToInvest)
                                  .CombineLatest(this.WhenAnyValue(model => model.SelectedPattern),
                                      (amount, pattern) => GetStageBreakdowns(fullProject, amount, pattern));
            Details = this.WhenAnyValue(model => model.AmountToInvest).Select(GetTransactionDetails);
            IsValid = this.WhenAnyValue(model => model.AmountToInvest).NotNull();
        }

        public string ProjectTitle => fullProject.Name;
        public decimal Progress => fullProject.RaisedAmount.Btc / fullProject.TargetAmount.Btc;
        public IAmountUI Raised => fullProject.RaisedAmount;

        public IObservable<IEnumerable<Breakdown>> StageBreakdowns { get; }
        public IObservable<TransactionDetails> Details { get; }
        public string ProjectId => fullProject.ProjectId.Value;

        public IEnumerable<IAmountUI> AmountPresets { get; } =
            [AmountUI.FromBtc(0.001), AmountUI.FromBtc(0.01), AmountUI.FromBtc(0.1), AmountUI.FromBtc(0.5)];

        public IEnumerable<DynamicStagePattern> AvailablePatterns { get; }
        public bool ShowPatternSelector { get; }

        public IObservable<object> Footer => Observable.Return(
            new FooterViewModel(
                fullProject,
                this.WhenAnyValue(model => model.AmountToInvest).Select(ui => ui ?? AmountUI.FromBtc(0)),
                this.WhenAnyValue(model => model.SelectedPattern),
                uiServices,
                investmentAppService,
                shell,
                walletContext));

        public IObservable<object> Header => Observable.Return(new HeaderViewModel(fullProject));
        public IObservable<bool> IsValid { get; }

        private static IEnumerable<Breakdown> GetStageBreakdowns(IFullProject fullProject, IAmountUI? amount, DynamicStagePattern? pattern)
        {
            var investAmount = amount ?? AmountUI.FromBtc(0m);

            // For Fund/Subscribe projects with a selected pattern, generate synthetic stages
            if (pattern != null && fullProject.ProjectType is ProjectType.Fund or ProjectType.Subscribe)
            {
                return GenerateStagesFromPattern(pattern, investAmount).ToList();
            }

            // For Invest projects, use the static stages from the project
            return fullProject.Stages.Select(stage => new Breakdown(
                                                 investAmount,
                                                 stage.RatioOfTotal,
                                                 stage.ReleaseDate)).ToList();
        }

        private static IEnumerable<Breakdown> GenerateStagesFromPattern(DynamicStagePattern pattern, IAmountUI investAmount)
        {
            int stageCount = pattern.StageCount;
            if (stageCount <= 0)
                return [];

            decimal ratioPerStage = 1m / stageCount;
            var now = DateTimeOffset.UtcNow;

            return Enumerable.Range(0, stageCount)
                .Select(i => new Breakdown(
                    investAmount,
                    ratioPerStage,
                    ComputeReleaseDate(now, pattern, i + 1)));
        }

        private static DateTimeOffset ComputeReleaseDate(DateTimeOffset startDate, DynamicStagePattern pattern, int stageNumber)
        {
            return pattern.PayoutDayType switch
            {
                PayoutDayType.FromStartDate => AddFrequencyIntervals(startDate, pattern.Frequency, stageNumber),
                PayoutDayType.SpecificDayOfMonth => ComputeSpecificDayOfMonth(startDate, pattern, stageNumber),
                PayoutDayType.SpecificDayOfWeek => ComputeSpecificDayOfWeek(startDate, pattern, stageNumber),
                _ => AddFrequencyIntervals(startDate, pattern.Frequency, stageNumber)
            };
        }

        private static DateTimeOffset AddFrequencyIntervals(DateTimeOffset startDate, StageFrequency frequency, int intervals)
        {
            return frequency switch
            {
                StageFrequency.Weekly => startDate.AddDays(7 * intervals),
                StageFrequency.Biweekly => startDate.AddDays(14 * intervals),
                StageFrequency.Monthly => startDate.AddMonths(intervals),
                StageFrequency.BiMonthly => startDate.AddMonths(2 * intervals),
                StageFrequency.Quarterly => startDate.AddMonths(3 * intervals),
                _ => startDate.AddMonths(intervals)
            };
        }

        private static DateTimeOffset ComputeSpecificDayOfMonth(DateTimeOffset startDate, DynamicStagePattern pattern, int stageNumber)
        {
            int monthsToAdd = pattern.Frequency switch
            {
                StageFrequency.Monthly => stageNumber,
                StageFrequency.BiMonthly => 2 * stageNumber,
                StageFrequency.Quarterly => 3 * stageNumber,
                _ => stageNumber
            };

            var target = startDate.AddMonths(monthsToAdd);
            int day = Math.Min(pattern.PayoutDay, DateTime.DaysInMonth(target.Year, target.Month));
            return new DateTimeOffset(target.Year, target.Month, day, 0, 0, 0, target.Offset);
        }

        private static DateTimeOffset ComputeSpecificDayOfWeek(DateTimeOffset startDate, DynamicStagePattern pattern, int stageNumber)
        {
            int weeksToAdd = pattern.Frequency switch
            {
                StageFrequency.Weekly => stageNumber,
                StageFrequency.Biweekly => 2 * stageNumber,
                _ => stageNumber
            };

            var target = startDate.AddDays(7 * weeksToAdd);
            // Adjust to the specified day of week
            int currentDay = (int)target.DayOfWeek;
            int targetDay = pattern.PayoutDay;
            int diff = targetDay - currentDay;
            return target.AddDays(diff);
        }

        private static TransactionDetails GetTransactionDetails(IAmountUI? amount)
        {
            if (amount == null)
            {
                return TransactionDetails.Empty();
            }

            AmountUI angorFee = new((long)Math.Ceiling(amount.Sats * 0.01));
            return new TransactionDetails(amount, new AmountUI(0), angorFee);
        }
    }
}
