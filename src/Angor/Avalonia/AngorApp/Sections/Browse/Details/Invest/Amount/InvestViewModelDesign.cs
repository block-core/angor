using System.Reactive.Linq;
using System.Windows.Input;

namespace AngorApp.Sections.Browse.Details.Invest.Amount;

public class AmountViewModelDesign : IAmountViewModel
{
    public ICommand Next { get; } = ReactiveCommand.Create(() => { });

    public decimal? Amount { get; set; } = 0.001m;
    public IObservable<bool> IsValid => Observable.Return(true);
    public IObservable<bool> IsBusy => Observable.Return(false);
    public bool AutoAdvance => false;
}