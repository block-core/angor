using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI;
using ReactiveUI;

namespace AngorApp.Sections.Portfolio.Penalties;

public class PenaltiesViewModelSample : IPenaltiesViewModel
{
    public PenaltiesViewModelSample()
    {
        GoToRecovery = ReactiveCommand.Create(() => { });
        var penalties = new ObservableCollection<IPenaltyViewModel>
        {
            new PenaltyViewModelSample(),
            new PenaltyViewModelSample(),
            new PenaltyViewModelSample()
        };
        Penalties = new ReadOnlyObservableCollection<IPenaltyViewModel>(penalties);
        Load = ReactiveCommand.CreateFromTask(() => Task.FromResult(Result.Success<IEnumerable<IPenaltyViewModel>>(Penalties))).Enhance();
    }

    public ICommand GoToRecovery { get; }

    public IReadOnlyCollection<IPenaltyViewModel> Penalties { get; }

    public IEnhancedCommand<Result<IEnumerable<IPenaltyViewModel>>> Load { get; }
}
