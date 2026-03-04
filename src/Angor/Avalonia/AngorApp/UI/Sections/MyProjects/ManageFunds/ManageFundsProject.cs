using Angor.Sdk.Funding.Shared;
using Angor.Shared.Models;
using AngorApp.Model.Contracts.Projects;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds;

public record ManageFundsProject(
    ProjectId ProjectId,
    string Name,
    IAmountUI RaisedAmount,
    IAmountUI TargetAmount,
    ProjectType ProjectType,
    IReadOnlyList<IStage> Stages,
    int AvailableTransactions) : IManageFundsProject
{
    public static ManageFundsProject From(IFullProject fullProject)
    {
        return new ManageFundsProject(
            fullProject.ProjectId,
            fullProject.Name,
            fullProject.RaisedAmount,
            fullProject.TargetAmount,
            fullProject.ProjectType,
            fullProject.Stages.ToList(),
            fullProject.AvailableTransactions);
    }
}
