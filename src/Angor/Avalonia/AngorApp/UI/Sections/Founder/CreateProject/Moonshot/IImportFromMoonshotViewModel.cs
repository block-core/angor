using Angor.Contexts.Funding.Founder.Dtos;

namespace AngorApp.UI.Sections.Founder.CreateProject.Moonshot;

public interface IImportFromMoonshotViewModel
{
    string? EventId { get; set; }

    bool IsLoading { get; }

    string? ErrorMessage { get; }

    IEnhancedCommand<Result<MoonshotProjectData>> Import { get; }

    IObservable<bool> IsValid { get; }
}