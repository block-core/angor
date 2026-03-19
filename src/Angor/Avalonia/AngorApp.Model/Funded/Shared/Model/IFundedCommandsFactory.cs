using AngorApp.Model.ProjectsV2;

namespace AngorApp.Model.Funded.Shared.Model;

public interface IFundedCommandsFactory
{
    IFundedCommands Create(IProject project, IInvestorData investorData);
}
