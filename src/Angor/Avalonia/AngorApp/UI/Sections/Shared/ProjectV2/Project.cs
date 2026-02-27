using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Sdk.Funding.Shared;

namespace AngorApp.UI.Sections.Shared.ProjectV2
{
    public abstract class Project(ProjectDto seed) : IProject
    {
        public string Name { get;  } = seed.Name;
        public string Description { get; } = seed.ShortDescription;
        public Uri? BannerUrl { get; } = seed.Banner;
        public Uri? LogoUrl { get; } = seed.Avatar;
        public ProjectId Id { get; } = seed.Id;
        public abstract IEnhancedCommand Refresh { get; }

        public static IProject Create(ProjectDto seed, IProjectAppService projectAppService)
        {
            return seed.ProjectType switch
            {
                Angor.Shared.Models.ProjectType.Invest => new InvestmentProject(seed, projectAppService),
                Angor.Shared.Models.ProjectType.Fund => new FundProject(seed, projectAppService),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}