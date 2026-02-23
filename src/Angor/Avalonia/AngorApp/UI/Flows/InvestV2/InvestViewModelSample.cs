using System.Windows.Input;
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
    }
}
