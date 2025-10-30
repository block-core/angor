using AngorApp.Model.Domain.Projects;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;

namespace AngorApp.Core.Factories;

public interface IProjectInvestCommandFactory
{
    IEnhancedCommand<Result<Maybe<Unit>>> Create(FullProject project, bool isInsideInvestmentPeriod);
}
