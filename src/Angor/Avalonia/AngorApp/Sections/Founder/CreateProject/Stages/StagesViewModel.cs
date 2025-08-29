using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.CreateProject.Stages;

public class StagesViewModel : ReactiveValidationObject, IStagesViewModel
{
    private readonly CompositeDisposable disposable = new();
    private readonly Func<DateTime?> getEndDate;
    private readonly IObservable<DateTime?> endDateChanges;

    public IEnhancedCommand AddStage { get; }

    private readonly SourceCache<ICreateProjectStage, long> stagesSource;

    public StagesViewModel(Func<DateTime?> getEndDate, IObservable<DateTime?> endDateChanges)
    {
        this.getEndDate = getEndDate;
        this.endDateChanges = endDateChanges;

        stagesSource = new SourceCache<ICreateProjectStage, long>(stage => stage.GetHashCode())
            .DisposeWith(disposable);

        var changes = stagesSource.Connect();

        LastStageDate = changes
            .AutoRefresh(stage => stage.ReleaseDate)
            .ToCollection()
            .Select(list => list.Select(s => s.ReleaseDate).Where(d => d.HasValue).DefaultIfEmpty().Max())
            .Replay(1)
            .RefCount();

        changes
            .Bind(out var stages)
            .Subscribe()
            .DisposeWith(disposable);

        Stages = stages;

        AddStage = ReactiveCommand.Create(() =>
            {
                stagesSource.AddOrUpdate(CreateStage());
                RecalculatePercentages();
            })
            .Enhance()
            .DisposeWith(disposable);

        var allStagesValid = changes
            .ToCollection()
            .Select(stages => stages.Count > 0 ? stages.Select(stage => stage.IsValid()).CombineLatest(validities => validities.All(v => v)) : Observable.Return(false))
            .Switch()
            .StartWith(false)
            .DistinctUntilChanged();

        this.ValidationRule(allStagesValid, b => b, _ => "Stages are not valid").DisposeWith(disposable);

        var totalPercent = changes
            .AutoRefresh(stage => stage.Percent)
            .ToCollection()
            .Select(list => list.Sum(stage => stage.Percent ?? 0));

        this.ValidationRule(totalPercent, percent => Math.Abs(percent - 100) < 1, _ => "Stage percentages should sum to 100%").DisposeWith(disposable);

        stagesSource.AddOrUpdate(CreateStage());
        RecalculatePercentages();
    }

    public ICollection<ICreateProjectStage> Stages { get; }

    private CreateProjectStage CreateStage()
    {
        return new CreateProjectStage(stage =>
        {
            stagesSource.Remove(stage);
            RecalculatePercentages();
        }, endDateChanges)
        {
            Percent = 100,
            ReleaseDate = (getEndDate() ?? DateTime.Now).AddDays(7)
        };
    }

    private void RecalculatePercentages()
    {
        var count = stagesSource.Count;
        if (count == 0)
        {
            return;
        }

        var percent = 100m / count;
        foreach (var stage in stagesSource.Items)
        {
            stage.Percent = percent;
        }
    }

    protected override void Dispose(bool disposing)
    {
        disposable.Dispose();
        base.Dispose(disposing);
    }

    public IObservable<bool> IsValid => this.IsValid();
    public IObservable<DateTime?> LastStageDate { get; }
}