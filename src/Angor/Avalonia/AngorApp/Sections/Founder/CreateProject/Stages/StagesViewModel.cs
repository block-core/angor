using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.CreateProject.Stages;

public class StagesViewModel : ReactiveValidationObject, IStagesViewModel
{
    private readonly CompositeDisposable disposable = new();
    
    public IEnhancedCommand AddStage { get; }
    
    private readonly SourceCache<ICreateProjectStage, long> stagesSource;

    public StagesViewModel()
    {
        stagesSource = new SourceCache<ICreateProjectStage, long>(stage => stage.GetHashCode())
            .DisposeWith(disposable);

        var changes = stagesSource.Connect();
        
        changes
            .Bind(out var stages)
            .Subscribe()
            .DisposeWith(disposable);
        
        Stages = stages;
        
        AddStage = ReactiveCommand.Create(() => stagesSource.AddOrUpdate(CreateStage())).Enhance()
            .DisposeWith(disposable);
        
        // Combinar directamente los observables IsValid() de cada stage
        var allStagesValid = changes
            .ToCollection()
            .Select(stages => stages.Count > 0 ? 
                stages.Select(stage => stage.IsValid()).CombineLatest(validities => validities.All(v => v)) :
                Observable.Return(false))
            .Switch()
            .StartWith(false)
            .DistinctUntilChanged();
        
        this.ValidationRule(allStagesValid, b => b, _ => "Stages are not valid").DisposeWith(disposable);
        
        var totalPercent = changes
            .AutoRefresh(stage => stage.Percent)
            .ToCollection()
            .Select(list => list.Sum(stage => stage.Percent ?? 0));
        
        this.ValidationRule(totalPercent, percent => Math.Abs(percent - 100) < 1, _ => "Stages percentajes should sum to 100%").DisposeWith(disposable);
        
        stagesSource.AddOrUpdate(CreateStage());
    }

    public ICollection<ICreateProjectStage> Stages { get; }

    private CreateProjectStage CreateStage()
    {
        return new CreateProjectStage(stage => stagesSource.Remove(stage))
        {
            Percent = 100,
            ReleaseDate = DateTime.Now
        };
    }

    protected override void Dispose(bool disposing)
    {
        disposable.Dispose();
        base.Dispose(disposing);
    }

    public IObservable<bool> IsValid => this.IsValid();
}