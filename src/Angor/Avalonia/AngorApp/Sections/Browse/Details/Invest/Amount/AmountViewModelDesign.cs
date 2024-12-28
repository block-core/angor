using System.Reactive.Linq;
using System.Windows.Input;
using AngorApp.Model;

namespace AngorApp.Sections.Browse.Details.Invest.Amount;

public class AmountViewModelDesign : IAmountViewModel
{
    public ICommand Next { get; } = ReactiveCommand.Create(() => { });
    public ulong? Amount { get; set; } = 20000;
    public decimal? AmountInBtc { get; set; }
    public IProject Project { get; } = new ProjectDesign();
    public IObservable<bool> IsValid => Observable.Return(true);
    public IObservable<bool> IsBusy => Observable.Return(false);
    public bool AutoAdvance => false;
}