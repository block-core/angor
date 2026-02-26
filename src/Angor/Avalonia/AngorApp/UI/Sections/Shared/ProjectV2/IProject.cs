using Angor.Sdk.Funding.Shared;

namespace AngorApp.UI.Sections.Shared.ProjectV2
{
    public interface IProject
    {
        string Name { get;  }
        string Description { get; }
        Uri? BannerUrl { get; }
        Uri? LogoUrl { get; }
        ProjectId Id { get; }
        IEnhancedCommand Refresh { get; }
    }
}