using System.Reactive.Linq;
using System.Windows.Input;

namespace AngorApp.Sections.Browse.Details.Invest;

public class InvestViewModelDesign : IInvestViewModel
{
    public ICommand Next { get; } = ReactiveCommand.Create(() => { });
    
    public decimal Amount { get; } = 0.001m;
    public IObservable<bool> IsBusy { get; } = Observable.Return(true);
}

public interface IInvestViewModel
{
    public ICommand Next { get; }
    public decimal Amount { get; }
    public IObservable<bool> IsBusy { get; }
}