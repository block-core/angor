using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
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
        
        stagesSource.Connect()
            .Bind(out var stages)
            .Subscribe()
            .DisposeWith(disposable);
        
        stagesSource.AddOrUpdate(CreateStage());
        Stages = stages;
        
        AddStage = ReactiveCommand.Create(() => stagesSource.AddOrUpdate(CreateStage())).Enhance()
            .DisposeWith(disposable);
        
        var isAllStagesValid = stagesSource.Connect()
            .AutoRefreshOnObservable(c => c.IsValid())   
            .ToCollection()
            .Select(x => x.All(c => c.ValidationContext.IsValid)) 
            .StartWith(false);
        
        this.ValidationRule(isAllStagesValid, b => b, _ => "Stages are not valid").DisposeWith(disposable);
        
        var totalPercent = stagesSource.Connect()
            .AutoRefresh(stage => stage.Percent)
            .ToCollection()
            .Select(list => list.Sum(stage => stage.Percent ?? 0));
        
        this.ValidationRule(totalPercent, percent => Math.Abs(percent - 100) < 1, _ => "Stages percentajes should sum to 100%").DisposeWith(disposable);
    }

    public IEnumerable<ICreateProjectStage> Stages { get; }

    private CreateProjectStage CreateStage()
    {
        return new CreateProjectStage(stage => stagesSource.Remove(stage));
    }

    protected override void Dispose(bool disposing)
    {
        disposable.Dispose();
        base.Dispose(disposing);
    }

    public IObservable<bool> IsValid => this.IsValid();
}