using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.CreateProject;

public interface ICreateProjectViewModel
{
    public IEnumerable<ICreateProjectStage> Stages { get; }
    public DateTime StartDate { get; }
    public DateTime? EndDate { get; set; }
    public IEnhancedCommand AddStage { get; }
    public int? PenaltyDays { get; set; }
    public DateTime? ExpiryDate { get; set; }
    IEnhancedCommand Create { get; }

    /// <inheritdoc cref="CreateProjectViewModel.websiteUri"/>
    string? WebsiteUri
    {
        get;
        [global::System.Diagnostics.CodeAnalysis.MemberNotNull("websiteUri")]
        set;
    }

    /// <inheritdoc cref="CreateProjectViewModel.description"/>
    string? Description
    {
        get;
        [global::System.Diagnostics.CodeAnalysis.MemberNotNull("description")]
        set;
    }

    /// <inheritdoc cref="CreateProjectViewModel.avatarUri"/>
    string? AvatarUri
    {
        get;
        [global::System.Diagnostics.CodeAnalysis.MemberNotNull("avatarUri")]
        set;
    }

    /// <inheritdoc cref="CreateProjectViewModel.bannerUri"/>
    string? BannerUri
    {
        get;
        [global::System.Diagnostics.CodeAnalysis.MemberNotNull("bannerUri")]
        set;
    }

    /// <inheritdoc cref="CreateProjectViewModel.projectName"/>
    string? ProjectName
    {
        get;
        [System.Diagnostics.CodeAnalysis.MemberNotNull("projectName")]
        set;
    }

    public long? Sats { get; set; }
}