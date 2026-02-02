namespace AngorApp.Core.Factories;

public interface IProjectInvestCommandFactory
{
    IEnhancedCommand<Result<Unit>> Create(FullProject project, bool isInsideInvestmentPeriod);
}
