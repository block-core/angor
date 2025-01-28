using System.Reactive.Linq;
using System.Windows.Input;
using Angor.UI.Model;

namespace AngorApp.Sections.Browse.Details.Invest.Amount;

public class AmountViewModelDesign : IAmountViewModel
{
    public ICommand Next { get; } = ReactiveCommand.Create(() => { });
    public long? Amount { get; set; } = 20000;
    public decimal? AmountInBtc { get; set; }
    public IProject Project { get; } = new ProjectDesign();
    public IObservable<bool> IsValid => Observable.Return(true);
    public IObservable<bool> IsBusy => Observable.Return(false);
    public bool AutoAdvance => false;
}