using System.Linq;
using DynamicData;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.CreateProject;

public partial class CreateProjectViewModel : ReactiveValidationObject, ICreateProjectViewModel
{
    [Reactive] private int? penaltyDays = 60;
    [Reactive] private DateTime? endDate;
    [Reactive] private DateTime? expiryDate;
    [Reactive] private string? websiteUri;
    [Reactive] private string? description;
    [Reactive] private string? avatarUri;
    [Reactive] private string? bannerUri;
    [Reactive] private string? projectName;
    [Reactive] private long? sats;

    private readonly SourceCache<ICreateProjectStage, long> stagesSource;

    public CreateProjectViewModel()
    {
        stagesSource = new SourceCache<ICreateProjectStage, long>(stage => stage.GetHashCode());
        AddStage = ReactiveCommand.Create(() => stagesSource.AddOrUpdate(CreateStage())).Enhance();
        stagesSource.Connect()
            .Bind(out var stages)
            .Subscribe();

        stagesSource.AddOrUpdate(CreateStage());
        Stages = stages;
        
        this.ValidationRule(x => x.EndDate, x => x != null, "Enter a date");
        this.ValidationRule(x => x.ExpiryDate, x => x != null, "Enter a date");
        this.ValidationRule(x => x.PenaltyDays, x => x >= 0, "Should be greater than 0");
        this.ValidationRule(x => x.ProjectName, x => !string.IsNullOrEmpty(x), "Cannot be empty");
        this.ValidationRule(x => x.Description, x => !string.IsNullOrEmpty(x), "Cannot be empty");
        this.ValidationRule(x => x.WebsiteUri, x => !string.IsNullOrEmpty(x) && !string.IsNullOrEmpty(x), "Cannot be empty");
        this.ValidationRule(x => x.BannerUri, x => !string.IsNullOrEmpty(x) && !string.IsNullOrEmpty(x), "Cannot be empty");
        this.ValidationRule(x => x.AvatarUri, x => !string.IsNullOrEmpty(x) && !string.IsNullOrEmpty(x), "Cannot be empty");
        this.ValidationRule(x => x.WebsiteUri, x => string.IsNullOrWhiteSpace(x) || Uri.TryCreate(x, UriKind.Absolute, out _), "Invalid URL");
        this.ValidationRule(x => x.BannerUri, x => string.IsNullOrWhiteSpace(x) || Uri.TryCreate(x, UriKind.Absolute, out _), "Invalid URL");
        this.ValidationRule(x => x.AvatarUri, x => string.IsNullOrWhiteSpace(x) || Uri.TryCreate(x, UriKind.Absolute, out _), "Invalid URL");
        this.ValidationRule(x => x.Sats, x => x is null or > 0, _ => "Amount must be greater than zero");
        this.ValidationRule(x => x.Sats, x => x is not null, _ => "Please, specify an amount");
        
        var obs = stagesSource.Connect()
            .AutoRefreshOnObservable(c => c.IsValid())   
            .ToCollection()
            .Select(lista => lista.All(c => c.ValidationContext.IsValid)) 
            .StartWith(false);

        this.ValidationRule(obs, b => b, _ => "Stages are not valid");
        
        var totalPercent = stagesSource.Connect()
            .AutoRefresh(stage => stage.Percent)
            .ToCollection()
            .Select(list => list.Sum(stage => stage.Percent ?? 0));
        
        this.ValidationRule(totalPercent, percent => Math.Abs(percent - 100) < 1, _ => "Stages percentajes should sum to 100%");

        Create = ReactiveCommand.Create(() => { }, this.IsValid()).Enhance();
    }

    public IEnhancedCommand AddStage { get; }

    public IEnhancedCommand Create { get; }

    public IEnumerable<ICreateProjectStage> Stages { get; }

    public DateTime StartDate { get; } = DateTime.Now;

    private CreateProjectStage CreateStage()
    {
        return new CreateProjectStage(stage => stagesSource.Remove(stage));
    }
}