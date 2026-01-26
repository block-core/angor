using System.Reactive.Disposables;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Model
{
    public partial class PayoutConfig : ReactiveValidationObject, IPayoutConfig
    {
        private readonly CompositeDisposable disposable = new();
        [Reactive] private DateTime? payoutDate;
        [Reactive] private decimal? percent;

        public PayoutConfig()
        {
            this.ValidationRule(x => x.PayoutDate, x => x != null, "Payout date is required")
                .DisposeWith(disposable);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                disposable.Dispose();
            }
            base.Dispose(disposing);
        }

        public IObservable<bool> IsValid => this.IsValid();
    }
}
