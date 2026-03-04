using Angor.Sdk.Funding.Shared;

namespace AngorApp.Model.ProjectsV2
{
    public interface IProject
    {
        string Name { get; }
        string Description { get; }
        Uri? BannerUrl { get; }
        Uri? LogoUrl { get; }
        ProjectId Id { get; }
        string FounderPubKey { get; }
        string NostrNpubKeyHex { get; }
        Uri? InformationUri { get; }
        IEnhancedCommand<Result> Invest { get; }
        IEnhancedCommand ManageFunds { get; }
        IEnhancedCommand Refresh { get; }
    }
}
