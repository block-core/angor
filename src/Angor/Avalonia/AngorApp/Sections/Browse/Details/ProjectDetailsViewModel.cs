using System;
using System.Collections.Generic;
using Angor.Contexts.Funding.Investor;
using AngorApp.Model.Domain.Projects;
using AngorApp.Core.Factories;
using AngorApp.UI.Controls.Common.FoundedProjectOptions;
using ReactiveUI;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;

namespace AngorApp.Sections.Browse.Details;

public class ProjectDetailsViewModel : ReactiveObject, IProjectDetailsViewModel
{
    private readonly FullProject project;

    public ProjectDetailsViewModel(
        FullProject project,
        IProjectInvestCommandFactory investCommandFactory,
        IFoundedProjectOptionsViewModelFactory foundedProjectOptionsFactory)
    {
        this.project = project;

        IsInsideInvestmentPeriod = DateTime.Now <= project.FundingEndDate;
        Invest = investCommandFactory.Create(project, IsInsideInvestmentPeriod);
        FoundedProjectOptions = foundedProjectOptionsFactory.Create(project.ProjectId);
    }

    public bool IsInsideInvestmentPeriod { get; }
    public TimeSpan? NextRelease { get; }
    public IStage? CurrentStage { get; }
    public IFoundedProjectOptionsViewModel FoundedProjectOptions { get; }

    public IEnhancedCommand<Result<Maybe<Unit>>> Invest { get; }

    public IEnumerable<INostrRelay> Relays { get; } =
    [
        new NostrRelayDesign
        {
            Uri = new Uri("wss://relay.angor.io")
        },
        new NostrRelayDesign
        {
            Uri = new Uri("wss://relay2.angor.io")
        }
    ];

    public IFullProject Project => project;
}

