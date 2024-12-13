using System.Windows.Input;
using AngorApp.Sections.Browse.Details.Invest.TransactionPreview;
using AngorApp.Sections.Wallet;
using AngorApp.Services;
using Avalonia.Threading;
using CSharpFunctionalExtensions;
using ReactiveUI.SourceGenerators;
using Zafiro.Avalonia.Controls.Navigation;

namespace AngorApp.Sections.Browse.Details.Invest;

public partial class InvestViewModel : ReactiveObject, IInvestViewModel
{
    public Project Project { get; }
    public IObservable<bool> IsBusy { get; }

    [Reactive] private decimal amount;
    
    public InvestViewModel(IWallet wallet, Project project, UIServices uiServices, INavigator navigator)
    {
        Project = project;
        
        var next = ReactiveCommand.CreateFromTask(async () =>
        {
            var result = await wallet.CreateTransaction()
                .TapError(s => uiServices.NotificationService.Show(s, "Cannot create transaction"))
                .Tap(transaction => Dispatcher.UIThread.InvokeAsync(() => navigator.Go(() => new TransactionPreviewViewModel(transaction))));
            return result;
        });
        
        Next = next;

        IsBusy = next.IsExecuting;
    }

    public ICommand Next { get; }
}