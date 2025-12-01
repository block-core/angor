using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using AngorApp.UI.Sections.Founder.CreateProject.FundingStructure;
using AngorApp.UI.Sections.Founder.CreateProject.Stages.Creator;
using DynamicData;
using DynamicData.Aggregation;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Reactive;

namespace AngorApp.UI.Sections.Founder.CreateProject.Stages;

public class StagesViewModel : ReactiveValidationObject, IStagesViewModel
{
    private readonly CompositeDisposable disposable = new();
    private readonly BehaviorSubject<DateTime?> endDateSubject;
    private readonly bool enableProductionValidations;

    public IEnhancedCommand AddStage { get; }

    private readonly SourceCache<ICreateProjectStage, long> stagesSource;

    public StagesViewModel(IObservable<DateTime?> endDateChanges, UIServices uiServices)
    {
        enableProductionValidations = uiServices.EnableProductionValidations();

        endDateSubject = new BehaviorSubject<DateTime?>(null);
        endDateChanges.Subscribe(endDateSubject);

        stagesSource = new SourceCache<ICreateProjectStage, long>(stage => stage.GetHashCode())
            .DisposeWith(disposable);

        var childStagesChanges = stagesSource.Connect();

        LastStageDate = childStagesChanges
            .AutoRefresh(stage => stage.ReleaseDate)
            .ToCollection()
            .Select(list => list.Select(s => s.ReleaseDate).Where(d => d.HasValue).DefaultIfEmpty().Max())
            .ReplayLastActive();

        childStagesChanges
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

        var totalPercent = childStagesChanges
            .AutoRefresh(stage => stage.Percent)
            .ToCollection()
            .Select(collection => collection.Sum(stage => stage.Percent ?? 0));

        var childrenAreValid =
            childStagesChanges
                .FilterOnObservable(s => s.IsValid().StartWith(false).Select(v => !v))
                .IsEmpty()
                .DistinctUntilChanged();

        // STAGE VALIDATIONS
        // Always enforced: All stages must be valid
        this.ValidationRule(childrenAreValid, b => b, x => "All stages must be valid").DisposeWith(disposable);

        // Always enforced: At least 1 stage required
        this.ValidationRule(stagesSource.CountChanged, b => b > 0, _ => "Project must have at least one funding stage.").DisposeWith(disposable);

        // Production only: At least 3 stages required
        if (enableProductionValidations)
        {
            this.ValidationRule(stagesSource.CountChanged, b => b >= 3, _ => "Project must have at least 3 funding stages.").DisposeWith(disposable);
        }

        // Always enforced: Total percentages must sum to 100% (tolerance: 0.01%)
        this.ValidationRule(totalPercent, percent => Math.Abs(percent - 100) < 0.01m, _ => $"Stage percentages must sum to exactly 100%.").DisposeWith(disposable);

        StagesCreator = new StagesCreatorViewModel().DisposeWith(disposable);
        CreateStages = ReactiveCommand.CreateFromTask(async () =>
        {
            var create = Stages.Count == 0 || await uiServices.Dialog.ShowConfirmation("Create stages", "Do you want to replace the current stages with new ones?\n\nThis action can't be undone.").GetValueOrDefault(() => false);

            if (create)
            {
                CreateStagesFromCreator();
            }

        })
       .Enhance()
       .DisposeWith(disposable);

        ChangeCreatorStartDateOnEndDateChanges(endDateChanges).DisposeWith(disposable);

        Errors = new ErrorSummarizer(ValidationContext).DisposeWith(disposable).Errors;
    }

    private IDisposable ChangeCreatorStartDateOnEndDateChanges(IObservable<DateTime?> endDateChanges)
    {
        return endDateChanges
         .Where(time => time.HasValue)
            .Do(time => StagesCreator.SelectedInitialDate = time.Value.AddDays(1))
     .Subscribe();
    }

    public ICollection<string> Errors { get; }

    private void CreateStagesFromCreator()
    {
        var stageCount = StagesCreator.NumberOfStages!.Value;
        decimal stagePercent = 100 / new decimal(stageCount);
        var initialDate = StagesCreator.SelectedInitialDate!.Value;

        var timespan = StagesCreator.SelectedFrequency switch
        {
            PaymentFrequency.Daily => TimeSpan.FromDays(1),
            PaymentFrequency.Weekly => TimeSpan.FromDays(7),
            PaymentFrequency.Monthly => TimeSpan.FromDays(30),
            PaymentFrequency.Quarterly => TimeSpan.FromDays(90),
            _ => TimeSpan.FromDays(7)
        };

        var stages = Enumerable.Range(0, stageCount).Select(i =>
             {
                 return new CreateProjectStage(stage =>
            {
               stagesSource.Remove(stage);
               RecalculatePercentages();
           }, endDateSubject)
                 {
                     Percent = stagePercent,
                     ReleaseDate = initialDate.Add(timespan * i)
                 };
             });

        stagesSource.Edit(updater => updater.Load(stages));
    }

    public ICollection<ICreateProjectStage> Stages { get; }

    private CreateProjectStage CreateStage()
    {
        return new CreateProjectStage(stage =>
           {
               stagesSource.Remove(stage);
               RecalculatePercentages();
           }, endDateSubject)
        {
            Percent = 100,
            ReleaseDate = (endDateSubject.Value ?? DateTime.Now).AddDays(7)
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
    public IStagesCreatorViewModel StagesCreator { get; }
    public IEnhancedCommand CreateStages { get; }
}