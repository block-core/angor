using Angor.Sdk.Funding.Shared;
using Angor.Shared.Models;
using AngorApp.Model.Contracts.Projects;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds;

public interface IManageFundsProject
{
    ProjectId ProjectId { get; }
    string Name { get; }
    IAmountUI RaisedAmount { get; }
    IAmountUI TargetAmount { get; }
    ProjectType ProjectType { get; }
    IReadOnlyList<IStage> Stages { get; }
    int AvailableTransactions { get; }
}
