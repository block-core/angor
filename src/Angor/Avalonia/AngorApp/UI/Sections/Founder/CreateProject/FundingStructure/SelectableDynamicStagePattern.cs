using Angor.Shared.Models;
using ReactiveUI;

namespace AngorApp.UI.Sections.Founder.CreateProject.FundingStructure;

public class SelectableDynamicStagePattern : ReactiveObject
{
    private bool isSelected;

    public SelectableDynamicStagePattern(DynamicStagePattern pattern)
    {
        Pattern = pattern;
    }

    public DynamicStagePattern Pattern { get; }

    public bool IsSelected
    {
        get => isSelected;
        set => this.RaiseAndSetIfChanged(ref isSelected, value);
    }

    // Expose pattern properties for binding
    public byte PatternId => Pattern.PatternId;
    public string Name => Pattern.Name;
    public string Description => Pattern.Description;
    public StageFrequency Frequency => Pattern.Frequency;
    public int StageCount => Pattern.StageCount;
    public PayoutDayType PayoutDayType => Pattern.PayoutDayType;
    public int PayoutDay => Pattern.PayoutDay;
}
