using System.Windows.Input;
using AngorApp.Sections.Browse;

namespace AngorApp.Features.Invest.Amount;

public class AmountViewModelDesign : IAmountViewModel
{
    public ICommand Next { get; } = ReactiveCommand.Create(() => { });
    public long? Amount { get; set; } = 20000;
    public decimal? AmountInBtc { get; set; }
    public IProject Project { get; } = new ProjectDesign();

    public IEnumerable<StageBreakdown> StageBreakdowns { get; } = new List<StageBreakdown>
    {
        new StageBreakdown(1, 120, 0.2, DateTime.Now),
        new StageBreakdown(1, 120, 0.2, DateTime.Now),
        new StageBreakdown(1, 120, 0.2, DateTime.Now),
    };
    
    public IObservable<bool> IsValid => Observable.Return(true);
    public IObservable<bool> IsBusy => Observable.Return(false);
    public bool AutoAdvance => false;
}