using System.Linq;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.Features.Invest.Amount;

public partial class AmountViewModel : ReactiveValidationObject, IAmountViewModel
{
    [Reactive] private long? amount;
    [ObservableAsProperty] private IEnumerable<Breakdown> stageBreakdowns;
    [ObservableAsProperty] private long walletBalance;
    
    public AmountViewModel(IWallet wallet, IProject project)
    {
        Project = project;

        this.ValidationRule(x => x.Amount, x => x is null or > 0, _ => "Amount must be greater than zero");
        this.ValidationRule(x => x.Amount, x => x is not null, _ => "Please, specify an amount");
        
        var isValidAmount = this
            .WhenAnyValue(x => x.Amount)
            .WithLatestFrom(wallet.WhenAnyValue(x => x.Balance), (a, b) => a is null || a <= b);
        
        this.ValidationRule(x => x.Amount, isValidAmount, "Amount exceeds balance");

        stageBreakdownsHelper = this.WhenAnyValue(model => model.Amount)
            .WhereNotNull()
            .Select(l => project.Stages.Select(stage => new Breakdown(stage.Index, l!.Value, stage.RatioOfTotal, stage.ReleaseDate)))
            .ToProperty(this, x => x.StageBreakdowns);
    }

    public IProject Project { get; }
    public IObservable<bool> IsValid => this.IsValid();
    public bool AutoAdvance => false;
}