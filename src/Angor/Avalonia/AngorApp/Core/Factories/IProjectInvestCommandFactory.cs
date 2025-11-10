namespace AngorApp.Core.Factories;

public interface IProjectInvestCommandFactory
{
    IEnhancedCommand<Result<Maybe<Unit>>> Create(FullProject project, bool isInsideInvestmentPeriod);
}
