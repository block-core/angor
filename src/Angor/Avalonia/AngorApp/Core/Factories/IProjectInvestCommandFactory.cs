using Angor.Sdk.Funding.Shared;
using Angor.Shared.Models;

namespace AngorApp.Core.Factories;

public interface IProjectInvestCommandFactory
{
    IEnhancedCommand<Result> Create(ProjectId projectId, DateTimeOffset fundingStart, DateTimeOffset fundingEnd, ProjectType projectType);
}
