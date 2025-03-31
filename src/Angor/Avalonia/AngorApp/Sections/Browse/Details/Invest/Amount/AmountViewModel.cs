using Angor.Wallet.Application;
using Angor.Wallet.Domain;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.Sections.Browse.Details.Invest.Amount;

public partial class AmountViewModel : ReactiveValidationObject, IAmountViewModel
{
    [Reactive] private long? amount;

    public AmountViewModel(WalletId walletId, IWalletAppService walletAppService, IProject project)
    {
        LoadBalance = ReactiveCommand.CreateFromObservable(() => Observable.FromAsync(() => walletAppService.GetBalance(walletId)).Successes().Select(x => x.Value));
        
        Project = project;
        this.ValidationRule(x => x.Amount, this.WhenAnyValue(x => x.Amount), x => x is null or > 0, _ => "Amount must be greater than zero");
        this.ValidationRule(x => x.Amount, this.WhenAnyValue(x => x.Amount), x => x is not null, _ => "Please, specify an amount");
        var isValidAmount = this.WhenAnyValue(x => x.Amount).WithLatestFrom(LoadBalance, (a, b) => a is null || a <= b);
        this.ValidationRule(x => x.Amount, isValidAmount, "Amount exceeds balance");
        IsBusy = LoadBalance.IsExecuting;

        LoadBalance.Execute().Subscribe();
    }

    public ReactiveCommand<Unit,long> LoadBalance { get; }
    public IProject Project { get; }
    public IObservable<bool> IsValid => this.IsValid();
    public IObservable<bool> IsBusy { get; }
    public bool AutoAdvance => false;
    [ObservableAsProperty] private long walletBalance;
}