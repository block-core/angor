using System.Windows.Input;
using Angor.Shared.Models;
using AngorApp.UI.Flows.InvestV2.Footer;
using AngorApp.UI.Flows.InvestV2.Header;
using AngorApp.UI.Flows.InvestV2.Model;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.UI.Flows.InvestV2
{
    public class InvestViewModelSample : ReactiveValidationObject, IInvestViewModel
    {
        public InvestViewModelSample()
        {
            ProjectName = "Example Project";
            ProjectId = "b04c...8d1a";
            Amount = 100000;

            StageBreakdowns = Observable.Return(
                new List<Breakdown>
                {
                    new(new AmountUI(50000), 0.5m, DateTime.Now.AddDays(30)),
                    new(new AmountUI(25000), 0.25m, DateTime.Now.AddDays(60)),
                    new(new AmountUI(25000), 0.25m, DateTime.Now.AddDays(90))
                });

            Details = Observable.Return(
                new TransactionDetails(
                    new AmountUI(100000),
                    new AmountUI(500),
                    new AmountUI(1000)));

            Invest = ReactiveCommand.Create(() => { });
            Cancel = ReactiveCommand.Create(() => { });
            SelectAmount = ReactiveCommand.Create<long>(a => Amount = a);

            // Sample patterns for design-time preview
            AvailablePatterns = new List<DynamicStagePattern>
            {
                new()
                {
                    PatternId = 0,
                    Name = "3-Month Monthly",
                    Description = "3 monthly payments on the 1st of each month",
                    Frequency = StageFrequency.Monthly,
                    StageCount = 3
                },
                new()
                {
                    PatternId = 1,
                    Name = "6-Month Monthly",
                    Description = "6 monthly payments on the 1st of each month",
                    Frequency = StageFrequency.Monthly,
                    StageCount = 6
                },
                new()
                {
                    PatternId = 2,
                    Name = "12-Week Weekly",
                    Description = "12 weekly payments every Monday (~3 months)",
                    Frequency = StageFrequency.Weekly,
                    StageCount = 12
                }
            };
            SelectedPattern = AvailablePatterns.First();
            ShowPatternSelector = true;
        }

        public decimal? Amount { get; set; }
        public ICommand Invest { get; }
        public ICommand Cancel { get; }
        public ICommand SelectAmount { get; }
        public string ProjectName { get; }
        public IObservable<bool> IsValid => Observable.Return(true);

        public IAmountUI SelectedAmountPreset { get; set; }
        public string ProjectTitle { get; } = "Angor UX";
        public decimal Progress { get; } = 0.21m;
        public IAmountUI Raised { get; } = AmountUI.FromBtc(0.1234m);
        public IObservable<IEnumerable<Breakdown>> StageBreakdowns { get; }
        public IObservable<TransactionDetails> Details { get; }
        public string ProjectId { get; }

        public IEnumerable<IAmountUI> AmountPresets { get; } =
            [AmountUI.FromBtc(0.001), AmountUI.FromBtc(0.01), AmountUI.FromBtc(0.1), AmountUI.FromBtc(0.5)];

        public IAmountUI AmountToInvest { get; set; } = AmountUI.FromBtc(0.001m);
        public IObservable<object> Footer { get; } = Observable.Return(new FooterViewModelSample());
        public IObservable<object> Header { get; } = Observable.Return(new HeaderViewModelSample());
        
        public IEnumerable<DynamicStagePattern> AvailablePatterns { get; }
        public DynamicStagePattern? SelectedPattern { get; set; }
        public bool ShowPatternSelector { get; }
    }
}
