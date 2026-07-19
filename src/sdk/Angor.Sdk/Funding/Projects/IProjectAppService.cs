using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Projects.Operations;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using static Angor.Sdk.Funding.Founder.Operations.CreateProjectInfo;
using static Angor.Sdk.Funding.Founder.Operations.CreateProjectProfile;
using static Angor.Sdk.Funding.Founder.Operations.CreateProjectConstants.CreateProject;
using Angor.Sdk.Funding.Projects.Dtos;

namespace Angor.Sdk.Funding.Projects;

public interface IProjectAppService
{
    Task<Result<LatestProjects.LatestProjectsResponse>> Latest(LatestProjects.LatestProjectsRequest request);
    Task<Result<TryGetProject.TryGetProjectResponse>> TryGet(TryGetProject.TryGetProjectRequest request);
    Task<Result<GetProject.GetProjectResponse>> Get(GetProject.GetProjectRequest request);
    Task<Result<GetFounderProjects.GetFounderProjectsResponse>> GetFounderProjects(WalletId walletId);
    Task<Result<ScanFounderProjects.ScanFounderProjectsResponse>> ScanFounderProjects(WalletId walletId);
    Task<Result<CreateProjectProfileResponse>> CreateProjectProfile(WalletId walletId, ProjectSeedDto projectSeedDto, CreateProjectDto project);
    Task<Result<CreateProjectInfoResponse>> CreateProjectInfo(WalletId walletId, CreateProjectDto project, ProjectSeedDto projectSeedDto);
    Task<Result<CreateProjectResponse>> CreateProject(WalletId walletId, long selectedFee, CreateProjectDto project, string projectInfoEventId, ProjectSeedDto projectSeedDto);
    Task<Result<ProjectStatisticsDto>> GetProjectStatistics(ProjectId projectId);

    /// <summary>
    /// Gets per-investor share breakdown for a project, showing each investor's
    /// total investment, share percentage, and amount claimed by the founder.
    /// </summary>
    Task<Result<GetInvestorShares.GetInvestorSharesResponse>> GetInvestorShares(ProjectId projectId);
    Task<Result<GetProjectRelays.GetProjectRelaysResponse>> GetRelaysForNpubAsync(string nostrPubKey);
    Task<Result<GetProjectInfoJson.GetProjectInfoJsonResponse>> GetProjectInfoJson(ProjectId projectId);

    Task<Result<FetchProjectProfileData.FetchProjectProfileDataResponse>> FetchProjectProfileData(ProjectId projectId);

    /// <summary>
    /// Kick a background refresh of a project's cached profile metadata from relays.
    /// Call when a user lands on a project page; the refresh only runs when the cache
    /// is older than the freshness TTL unless <paramref name="force"/> is set.
    /// </summary>
    Task<Result<RevalidateProjectMetadata.RevalidateProjectMetadataResponse>> RevalidateProjectMetadata(ProjectId projectId, bool force = false);

    Task<Result<UpdateProjectProfile.UpdateProjectProfileResponse>> UpdateProjectProfile(
        WalletId walletId,
        ProjectId projectId,
        ProjectMetadata metadata,
        string? projectContent,
        IReadOnlyList<FaqItem>? faqItems,
        IReadOnlyList<string>? memberPubkeys,
        IReadOnlyList<MediaItem>? mediaItems);
}
