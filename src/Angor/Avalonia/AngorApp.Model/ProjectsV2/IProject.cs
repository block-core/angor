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
        DateTimeOffset FundingStart { get; }
        DateTimeOffset FundingEnd { get; }
        IEnhancedCommand Refresh { get; }
        IObservable<ProjectStatus> ProjectStatus { get; }
        IAmountUI FundingTarget { get; }
        IObservable<IAmountUI> FundingRaised { get; }
        IObservable<int> SupporterCount { get; }
    }
}